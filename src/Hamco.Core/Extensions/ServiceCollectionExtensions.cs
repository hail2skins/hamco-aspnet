using Hamco.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Hamco.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthServices(
        this IServiceCollection services,
        string jwtSecret,
        string jwtIssuer,
        string jwtAudience,
        int expirationMinutes = 60)
    {
        // Register services
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtService>(sp => 
            new JwtService(jwtSecret, jwtIssuer, jwtAudience, expirationMinutes));

        // Configure JWT authentication
        var key = Encoding.UTF8.GetBytes(jwtSecret);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false; // Set to true in production with HTTPS
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            // Support cookie-based tokens for browser sessions
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // Check cookie if no Authorization header
                    if (string.IsNullOrEmpty(context.Token))
                    {
                        context.Token = context.Request.Cookies["AuthToken"];
                    }
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();

        return services;
    }
}
