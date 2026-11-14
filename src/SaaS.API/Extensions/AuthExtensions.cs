using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SaaS.API.Authorization;
using SaaS.API.Options;
using SaaS.Application.Common.Abstractions;

namespace SaaS.API.Extensions;

public sealed class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtOptions _jwtOptions;

    public ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        if (string.IsNullOrWhiteSpace(_jwtOptions.SigningKey) || _jwtOptions.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 characters.");
        }

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(15),
            NameClaimType = "email",
            RoleClaimType = "platform_role"
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sessionClaim = context.Principal?.FindFirst("session_id")?.Value;
                if (!Guid.TryParse(sessionClaim, out var sessionId))
                {
                    context.Fail("Missing session identifier.");
                    return;
                }

                var sessionStore = context.HttpContext.RequestServices.GetRequiredService<ISessionStore>();
                var session = await sessionStore.FindSessionByIdAsync(sessionId, context.HttpContext.RequestAborted);
                if (session is null || !session.IsActive)
                {
                    context.Fail("Session has been revoked.");
                }
            }
        };
    }
}

public static class AuthExtensions
{
    public static IServiceCollection AddPlatformAuthenticationAndAuthorization(this IServiceCollection services)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                PlatformAdminOnlyAttribute.PolicyName,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PlatformAdminRequirement()));
        });

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, PlatformAdminAuthorizationHandler>();

        return services;
    }
}
