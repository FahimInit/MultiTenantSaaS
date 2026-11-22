using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using SaaS.API.Extensions;
using SaaS.API.Middleware;
using SaaS.API.Options;
using SaaS.Application;
using SaaS.Infrastructure;
using SaaS.Infrastructure.Persistence.Seed;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.LoadDotEnvIfDevelopment();
builder.Configuration.AddJsonFile("appsettings.Development.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddSerilog((services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName);

builder.Services
    .AddOptions<CorsOptions>()
    .BindConfiguration(CorsOptions.SectionName);

var allowedOrigins = builder.Configuration
    .GetSection($"{CorsOptions.SectionName}:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCorsPolicy", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddProblemDetails();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPlatformAuthenticationAndAuthorization()
    .AddPlatformApiVersioning()
    .AddPlatformSwagger()
    .AddPlatformRateLimiting(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("SeedDemoData:RunOnStartup"))
{
    await DatabaseSeeder.SeedDemoDataAsync(app.Services);
}

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UsePlatformSwagger();
}
else
{
    app.UseHsts();
}

app.UseCors("FrontendCorsPolicy");
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapControllers().Add(endpointBuilder =>
{
    var hasExplicitPolicy = endpointBuilder.Metadata.Any(m =>
        m is EnableRateLimitingAttribute or DisableRateLimitingAttribute);

    if (!hasExplicitPolicy)
    {
        endpointBuilder.Metadata.Add(new EnableRateLimitingAttribute(RateLimitingExtensions.ApiPolicyName));
    }
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

app.Run();

public partial class Program;
