using Asp.Versioning.ApiExplorer;
using Microsoft.OpenApi.Models;

namespace SaaS.API.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddPlatformSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Multi-Tenant SaaS Management Platform API",
                Version = "v1",
                Description = "Production-oriented Multi-Tenant B2B SaaS API built with Clean Architecture, PostgreSQL, and ASP.NET Core."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter your JWT access token."
            });

            options.AddSecurityDefinition("TenantHeader", new OpenApiSecurityScheme
            {
                Name = "X-Tenant-Id",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "Active organization UUID for tenant-scoped endpoints."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                },
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "TenantHeader" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    public static WebApplication UsePlatformSwagger(this WebApplication app)
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            foreach (var description in provider.ApiVersionDescriptions)
            {
                options.SwaggerEndpoint(
                    $"/swagger/{description.GroupName}/swagger.json",
                    $"SaaS Platform API {description.GroupName.ToUpperInvariant()}");
            }

            options.RoutePrefix = "swagger";
        });

        return app;
    }
}
