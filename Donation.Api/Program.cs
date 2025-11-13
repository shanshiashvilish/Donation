using Donation.Api.Middlewares;
using Donation.Api.Options;
using Donation.Core.Common;
using Donation.Infrastructure;
using Donation.Infrastructure.Clients.Flitt;
using Donation.Infrastructure.Clients.SendGrid;
using Donation.Infrastructure.Configuration;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Npgsql;
using OpenIddict.Validation.AspNetCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Donation.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            ConfigureHostForContainer(builder);
            var dbConnectionString = NormalizePgConnectionString();

            ConfigureServices(builder, dbConnectionString);

            var app = builder.Build();

            RegisterLifetimeLogs(app);

            ConfigureMiddleware(app);
            MapEndpoints(app);

            await ApplyMigrationsAsync(app.Services, dbConnectionString);

            await app.RunAsync();
        }

        #region Private Methods

        private static void ConfigureHostForContainer(WebApplicationBuilder builder)
        {
#if !DEBUG
            // Bind to platform port
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

            // Accept reverse proxy headers
            builder.Services.Configure<ForwardedHeadersOptions>(opts =>
            {
                opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                opts.KnownNetworks.Clear();
                opts.KnownProxies.Clear();
            });
#endif
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        private static void ConfigureServices(WebApplicationBuilder builder, string dbConnectionString)
        {
            var services = builder.Services;
            var config = builder.Configuration;

            // Controllers & JSON
            services.AddControllers(o => o.Filters.Add<ValidateModelFilter>())
                .AddJsonOptions(o =>
                {
                    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                });

            // Swagger
            AddSwagger(services);

            // EF Core (PostgreSQL + OpenIddict)
            services.AddDbContext<AppDbContext>(opt =>
            {
                opt.UseNpgsql(dbConnectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                });
                opt.UseOpenIddict();
            });

            // Domain services
            services.AddAuthServices();
            services.AddUserServices();
            services.AddOtpServices();
            services.AddSubscriptionrServices(); // keep exact name to avoid breaking your extensions
            services.AddPaymentServices();
            services.AddSendGridServices();
            services.AddFlittClientServices();

            services.AddScoped<ValidateModelFilter>();
            services.Configure<FlittOptions>(config.GetSection("Flitt"));
            services.Configure<SendGridOptions>(config.GetSection("SendGrid"));

            // OpenIddict
            AddOpenIddict(services, config);

            // AuthN/Z
            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            });
            services.AddAuthorization(o => o.AddPolicy("Donor", p => p.RequireRole(Roles.Donor)));

            // CORS (dev)
            services.AddCors(o => o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
        }

        private static void AddSwagger(IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Donation API", Version = "v1" });
                const string schemeId = "AuthHeader";
                c.AddSecurityDefinition(schemeId, new OpenApiSecurityScheme
                {
                    Description = "Paste exactly: Bearer {access_token}",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = schemeId }
                        },
                        Array.Empty<string>()
                    }
                });
            });
        }

        private static void AddOpenIddict(IServiceCollection services, IConfiguration config)
        {
            services.AddOpenIddict()
              .AddCore(opt => opt.UseEntityFrameworkCore().UseDbContext<AppDbContext>())
              .AddServer(opt =>
              {
                  opt.SetTokenEndpointUris("/auth");
                  opt.AllowCustomFlow("otp");
                  opt.AllowRefreshTokenFlow();
                  opt.DisableRollingRefreshTokens();
                  opt.AcceptAnonymousClients();
                  opt.AddDevelopmentEncryptionCertificate()
                     .AddDevelopmentSigningCertificate();
                  opt.DisableAccessTokenEncryption();

                  var tokenLifetimes = config.GetSection("Auth:Tokens").Get<TokenLifetimeOptions>()
                                      ?? new TokenLifetimeOptions { AccessTokenMinutes = 60, RefreshTokenHours = 24 };
                  opt.SetAccessTokenLifetime(TimeSpan.FromMinutes(tokenLifetimes.AccessTokenMinutes));
                  opt.SetRefreshTokenLifetime(TimeSpan.FromHours(tokenLifetimes.RefreshTokenHours));

                  opt.UseAspNetCore().EnableTokenEndpointPassthrough();

                  // minimal token shape for clients
                  opt.AddEventHandler<OpenIddict.Server.OpenIddictServerEvents.ApplyTokenResponseContext>(b =>
                  {
                      b.UseInlineHandler(ctx =>
                      {
                          ctx.Response!.SetParameter("bearerToken", ctx.Response.AccessToken);
                          ctx.Response.SetParameter("refreshToken", ctx.Response.RefreshToken);
                          ctx.Response.SetParameter("expiresIn", ctx.Response.ExpiresIn);

                          ctx.Response.AccessToken = null;
                          ctx.Response.RefreshToken = null;
                          ctx.Response.TokenType = null;
                          ctx.Response.ExpiresIn = null;
                          ctx.Response.Scope = null;
                          ctx.Response.IdToken = null;
                          return default;
                      });
                  });
              })
              .AddValidation(opt =>
              {
                  opt.UseLocalServer();
                  opt.UseAspNetCore();
              });
        }


        private static void ConfigureMiddleware(WebApplication app)
        {
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
            {
                app.UseSwagger();
                app.UseSwaggerUI(o =>
                {
                    o.SwaggerEndpoint("/swagger/v1/swagger.json", "Donation API v1");
                    o.DisplayRequestDuration();
                    o.ConfigObject.AdditionalItems["persistAuthorization"] = true;
                });
            }

            app.UseForwardedHeaders();

            // Only redirect to HTTPS when an HTTPS listener exists
            bool hasHttpsUrl = app.Urls.Any(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
            if (hasHttpsUrl)
                app.UseHttpsRedirection();
            else
                app.Logger.LogInformation("HTTPS redirection disabled (no HTTPS listener in container).");

            app.UseCors("AllowAll");
            app.UseAuthentication();
            app.UseAuthorization();
        }

        private static void MapEndpoints(WebApplication app)
        {
            app.MapControllers();
        }


        private static async Task ApplyMigrationsAsync(IServiceProvider services, string connStr)
        {
            using var scope = services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                var all = db.Database.GetMigrations().ToArray();
                if (all.Length == 0)
                {
                    logger.LogWarning("No migrations found. Ensuring database is created from the current model.");
                    await db.Database.EnsureCreatedAsync();
                    return;
                }

                var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
                if (pending.Length == 0)
                {
                    logger.LogInformation("No pending migrations. Database is up to date.");
                    return;
                }

                logger.LogInformation("Applying {Count} pending migrations: {Migrations}", pending.Length, string.Join(", ", pending));
                await db.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize the database. ConnStr: {Conn}", SafeMask(connStr));
                // throw; // uncomment to fail fast
            }
        }


        private static string NormalizePgConnectionString()
        {
            var dbConnectionUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
                                 ?? throw new InvalidOperationException("DATABASE_URL is missing.");

            var url = dbConnectionUrl.Trim();

            // Accept both URL and already-built connection strings
            if (!(url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                  url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)))
            {
                if (url.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                    url.Contains("Server=", StringComparison.OrdinalIgnoreCase))
                    return url;

                throw new ArgumentException("DATABASE_URL must start with postgres:// or postgresql://");
            }

            var uri = new Uri(url);
            var userInfo = Uri.UnescapeDataString(uri.UserInfo ?? "");
            var parts = userInfo.Split(':', 2);
            var user = parts.Length > 0 ? parts[0] : "";
            var pass = parts.Length > 1 ? parts[1] : "";
            var dbName = uri.AbsolutePath.TrimStart('/');

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.IsDefaultPort ? 5432 : uri.Port,
                Username = user,
                Password = pass,
                Database = dbName,
                SslMode = SslMode.Require
            };

            // pass-through query params (sslmode, pooling, etc.)
            var query = uri.Query.TrimStart('?');
            if (!string.IsNullOrEmpty(query))
            {
                foreach (var kv in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kvp = kv.Split('=', 2);
                    var key = Uri.UnescapeDataString(kvp[0]);
                    var val = kvp.Length > 1 ? Uri.UnescapeDataString(kvp[1]) : "";
                    builder[key] = val;
                }
            }

            return builder.ConnectionString;
        }

        private static string SafeMask(string cs)
        {
            try
            {
                var b = new NpgsqlConnectionStringBuilder(cs);
                if (!string.IsNullOrEmpty(b.Password))
                    b.Password = new string('*', Math.Min(b.Password.Length, 8));
                return b.ToString();
            }
            catch
            {
                return cs;
            }
        }

        private static void RegisterLifetimeLogs(WebApplication app)
        {
            app.Lifetime.ApplicationStarted.Register(() =>
                app.Logger.LogInformation("ApplicationStarted fired."));
            app.Lifetime.ApplicationStopping.Register(() =>
                app.Logger.LogWarning("ApplicationStopping signal received (platform likely sending SIGTERM)."));
            app.Lifetime.ApplicationStopped.Register(() =>
                app.Logger.LogWarning("ApplicationStopped fired."));
        }

        #endregion
    }
}
