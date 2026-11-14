using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Modules.Identity;
using SaaS.Application.Modules.Tenants;

namespace SaaS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Identity handlers
        services.AddScoped<ICommandHandler<RegisterUserCommand, RegisterUserResponse>, RegisterUserHandler>();
        services.AddScoped<ICommandHandler<VerifyEmailCommand>, VerifyEmailHandler>();
        services.AddScoped<ICommandHandler<ResendVerificationCommand>, ResendVerificationHandler>();
        services.AddScoped<ICommandHandler<LoginCommand, AuthTokenResponse>, LoginHandler>();
        services.AddScoped<ICommandHandler<RefreshTokenCommand, AuthTokenResponse>, RefreshTokenHandler>();
        services.AddScoped<ICommandHandler<LogoutCommand>, LogoutHandler>();
        services.AddScoped<ICommandHandler<ForgotPasswordCommand>, ForgotPasswordHandler>();
        services.AddScoped<ICommandHandler<ResetPasswordCommand>, ResetPasswordHandler>();
        services.AddScoped<IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>, GetCurrentUserHandler>();
        services.AddScoped<IQueryHandler<GetActiveSessionsQuery, IReadOnlyList<UserSessionDto>>, GetActiveSessionsHandler>();
        services.AddScoped<ICommandHandler<RevokeSessionCommand>, RevokeSessionHandler>();

        // Tenant handlers
        services.AddScoped<ICommandHandler<CreateTenantCommand, TenantDetailDto>, CreateTenantHandler>();
        services.AddScoped<IQueryHandler<GetMyTenantsQuery, IReadOnlyList<UserTenantSummaryDto>>, GetMyTenantsHandler>();
        services.AddScoped<IQueryHandler<GetCurrentTenantQuery, TenantDetailDto>, GetCurrentTenantHandler>();
        services.AddScoped<ICommandHandler<UpdateCurrentTenantCommand, TenantDetailDto>, UpdateCurrentTenantHandler>();
        services.AddScoped<ICommandHandler<CompleteOnboardingCommand, TenantDetailDto>, CompleteOnboardingHandler>();
        services.AddScoped<IQueryHandler<GetTenantSettingsQuery, TenantSettingsDto>, GetTenantSettingsHandler>();
        services.AddScoped<ICommandHandler<UpdateTenantSettingsCommand, TenantSettingsDto>, UpdateTenantSettingsHandler>();

        return services;
    }
}
