using Donation.Infrastructure;
using Donation.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
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
                c.SupportNonNullableReferenceTypes();

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


            // EF Core
            builder.Services.AddDbContext<AppDbContext>(opt =>
            {
                opt.UseInMemoryDatabase("donation-db");
                opt.UseOpenIddict();
            });

            // Domain services
            builder.Services.AddAuthServices();
            builder.Services.AddUserServices();
            builder.Services.AddOtpServices();

            // OpenIddict
            builder.Services.AddOpenIddict()
              .AddCore(opt =>
              {
                  opt.UseEntityFrameworkCore()
                     .UseDbContext<AppDbContext>();
              })
              .AddServer(opt =>
              {
                  opt.SetTokenEndpointUris("/auth/login");

                  opt.AllowCustomFlow("otp");
                  opt.AllowRefreshTokenFlow();
                  opt.AcceptAnonymousClients();
                  opt.AddDevelopmentEncryptionCertificate()
                     .AddDevelopmentSigningCertificate();
                  opt.DisableAccessTokenEncryption();
                  opt.UseAspNetCore()
                     .EnableTokenEndpointPassthrough();

              })
              .AddValidation(opt =>
              {
                  opt.UseLocalServer();
                  opt.UseAspNetCore();
              });

            // Authentication defaults (keep as you had)
            builder.Services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            });


            // Authorization (roles & policies)
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("User", policy => policy.RequireRole("User"));
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", p =>
                    p.AllowAnyOrigin()
                     .AllowAnyHeader()
                     .AllowAnyMethod());
            });

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(opts =>
                {
                    opts.SwaggerEndpoint("/swagger/v1/swagger.json", "Donation API v1");
                    opts.DisplayRequestDuration();
                    opts.ConfigObject.AdditionalItems["persistAuthorization"] = true;
                });
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
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
