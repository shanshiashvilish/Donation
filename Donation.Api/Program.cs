using Donation.Core.Common;
using Donation.Infrastructure;
using Donation.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenIddict.Validation.AspNetCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Donation.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Controllers & JSON
            builder.Services.AddControllers()
                .AddJsonOptions(o =>
                {
                    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
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
                    { new OpenApiSecurityScheme
                        { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = schemeId } },
                      Array.Empty<string>() }
                });
            });

            // EF Core
            builder.Services.AddDbContext<AppDbContext>(opt =>
            {
                opt.UseInMemoryDatabase("donation-db");
                opt.UseOpenIddict();
            });

            // Domain services (your DI ext methods)
            builder.Services.AddAuthServices();
            builder.Services.AddUserServices();
            builder.Services.AddOtpServices();

            // OpenIddict
            builder.Services.AddOpenIddict()
              .AddCore(opt =>
              {
                  opt.UseEntityFrameworkCore().UseDbContext<AppDbContext>();
              })
              .AddServer(opt =>
              {
                  opt.SetTokenEndpointUris("/auth/login");

                  opt.AllowCustomFlow("otp");
                  opt.AllowRefreshTokenFlow();
                  opt.AcceptAnonymousClients();

                  opt.AddDevelopmentEncryptionCertificate()
                     .AddDevelopmentSigningCertificate();

                  // Produce a signed JWT (not encrypted) so it's easy to use/debug
                  opt.DisableAccessTokenEncryption();

                  opt.UseAspNetCore().EnableTokenEndpointPassthrough();

                  // Shape the JSON response to only include your 3 fields
                  opt.AddEventHandler<OpenIddict.Server.OpenIddictServerEvents.ApplyTokenResponseContext>(b =>
                  {
                      b.UseInlineHandler(ctx =>
                      {
                          // copy standard values to custom keys
                          ctx.Response!.SetParameter("bearerToken", ctx.Response.AccessToken);
                          ctx.Response.SetParameter("refreshToken", ctx.Response.RefreshToken);
                          ctx.Response.SetParameter("expiresIn", ctx.Response.ExpiresIn);

                          // remove standard fields
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
                  opt.UseLocalServer(); // validate tokens issued by this server
                  opt.UseAspNetCore();
              });

            // Authentication
            builder.Services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            });

            // Authorization (simple role policies; see Roles class below)
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("Donor", p => p.RequireRole(Roles.Donor));
            });

            // Very open CORS for dev
            builder.Services.AddCors(o => o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(o =>
                {
                    o.SwaggerEndpoint("/swagger/v1/swagger.json", "Donation API v1");
                    o.DisplayRequestDuration();
                    o.ConfigObject.AdditionalItems["persistAuthorization"] = true;
                });
            }

            app.UseHttpsRedirection();
            app.UseCors("AllowAll");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
