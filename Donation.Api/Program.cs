using Donation.Api.Middlewares;
using Donation.Api.Options;
using Donation.Core.Common;
using Donation.Infrastructure;
using Donation.Infrastructure.Clients.Flitt;
using Donation.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenIddict.Validation.AspNetCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using Microsoft.AspNetCore.HttpOverrides;
using Donation.Infrastructure.Clients.SendGrid;

namespace Donation.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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

            # endif

            var dbConnectionString = GetNormalizedPgUrlToDbConnectionString();

            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            // Controllers & JSON
            builder.Services.AddControllers(o => o.Filters.Add<ValidateModelFilter>())
                .AddJsonOptions(o =>
                {
                    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
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

            // EF Core (PostgreSQL + OpenIddict)
            builder.Services.AddDbContext<AppDbContext>(opt =>
            {
                opt.UseNpgsql(dbConnectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                });
                opt.UseOpenIddict();
            });

            // Domain services
            builder.Services.AddAuthServices();
            builder.Services.AddUserServices();
            builder.Services.AddOtpServices();
            builder.Services.AddSubscriptionrServices();
            builder.Services.AddPaymentServices();
            builder.Services.AddSendGridServices();
            builder.Services.AddFlittClientServices();

            builder.Services.AddScoped<ValidateModelFilter>();
            builder.Services.Configure<FlittOptions>(builder.Configuration.GetSection("Flitt"));
            builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection("SendGrid"));

            // OpenIddict
            builder.Services.AddOpenIddict()
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

                  var tokenLifetimes = builder.Configuration.GetSection("Auth:Tokens").Get<TokenLifetimeOptions>()
                                      ?? new TokenLifetimeOptions { AccessTokenMinutes = 60, RefreshTokenHours = 24 };
                  opt.SetAccessTokenLifetime(TimeSpan.FromMinutes(tokenLifetimes.AccessTokenMinutes));
                  opt.SetRefreshTokenLifetime(TimeSpan.FromHours(tokenLifetimes.RefreshTokenHours));

                  opt.UseAspNetCore().EnableTokenEndpointPassthrough();

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

            // AuthN/Z
            builder.Services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            });
            builder.Services.AddAuthorization(o => o.AddPolicy("Donor", p => p.RequireRole(Roles.Donor)));

            // CORS (dev)
            builder.Services.AddCors(o =>
                o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

            var app = builder.Build();

            // Extra visibility on shutdown signals
            app.Lifetime.ApplicationStarted.Register(() =>
                app.Logger.LogInformation("ApplicationStarted fired."));
            app.Lifetime.ApplicationStopping.Register(() =>
                app.Logger.LogWarning("ApplicationStopping signal received (platform likely sending SIGTERM)."));
            app.Lifetime.ApplicationStopped.Register(() =>
                app.Logger.LogWarning("ApplicationStopped fired."));

            app.UseMiddleware<ExceptionHandlingMiddleware>();

            if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
            {
                //app.UseDeveloperExceptionPage();
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

            app.MapControllers();

            // Auto-apply migrations (or create schema)
            using (var scope = app.Services.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                try
                {
                    var all = db.Database.GetMigrations().ToArray();
                    if (all.Length != 0)
                    {
                        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
                        if (pending.Length != 0)
                        {
                            logger.LogInformation("Applying {Count} pending migrations: {Migrations}",
                                pending.Length, string.Join(", ", pending));
                            await db.Database.MigrateAsync();
                            logger.LogInformation("Migrations applied successfully.");
                        }
                        else
                        {
                            logger.LogInformation("No pending migrations. Database is up to date.");
                        }
                    }
                    else
                    {
                        logger.LogWarning("No migrations found. Ensuring database is created from the current model.");
                        await db.Database.EnsureCreatedAsync();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to initialize the database. ConnStr: {Conn}",
                        SafeMask(dbConnectionString));
                    // throw; // uncomment to fail fast
                }
            }

            await app.RunAsync();
        }

        private static string GetNormalizedPgUrlToDbConnectionString()
        {
            var dbConnectionUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (string.IsNullOrWhiteSpace(dbConnectionUrl))
                throw new InvalidOperationException("DATABASE_URL is missing.");

            var url = dbConnectionUrl.Trim();

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

            var builder = new Npgsql.NpgsqlConnectionStringBuilder
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
    }
}
