using Donation.Infrastructure;
using Donation.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;
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

            // Swagger (Swashbuckle)
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Donation API", Version = "v1" });
                //var securityScheme = new OpenApiSecurityScheme
                //{
                //    Name = "Authorization",
                //    Type = SecuritySchemeType.Http,
                //    Scheme = "bearer",
                //    BearerFormat = "JWT",
                //    In = ParameterLocation.Header,
                //    Description = "Enter: Bearer {your access token}"
                //};
                //c.AddSecurityDefinition("Bearer", securityScheme);
                //c.AddSecurityRequirement(new OpenApiSecurityRequirement
                //{
                //    { securityScheme, Array.Empty<string>() }
                //});
            });

            // EF Core
            builder.Services.AddDbContext<AppDbContext>(opt =>
            {
                opt.UseInMemoryDatabase("donation-db");
                opt.UseOpenIddict();
            });

            // Domain services
            builder.Services.AddUserServices();
            // builder.Services.AddScoped<IOtpService, OtpService>(); // enable when implemented

            // OpenIddict
            builder.Services.AddOpenIddict()
                .AddCore(options =>
                {
                    options.UseEntityFrameworkCore()
                           .UseDbContext<AppDbContext>();
                })
                .AddServer(options =>
                {
                    options.SetTokenEndpointUris("/connect/token");

                    options.AllowCustomFlow("otp");   // custom grant: grant_type=otp
                    options.AllowRefreshTokenFlow();  // refresh_token grant

                    options.DisableAccessTokenEncryption();

                    options.AddDevelopmentEncryptionCertificate()
                           .AddDevelopmentSigningCertificate();

                    options.UseAspNetCore()
                           .EnableTokenEndpointPassthrough();
                })
                .AddValidation(options =>
                {
                    options.UseLocalServer();
                    options.UseAspNetCore();
                });

            // Authentication (OpenIddict validation)
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            });

            // Authorization (roles & policies)
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("User", policy => policy.RequireRole("User"));
            });

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(setup =>
                {
                    setup.SwaggerEndpoint("/swagger/v1/swagger.json", "Donation API v1");
                    setup.DisplayRequestDuration();
                });
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
