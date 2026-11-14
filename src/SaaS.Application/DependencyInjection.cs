using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Models;
using SaaS.Application.Modules.Audit;
using SaaS.Application.Modules.Dashboard;
using SaaS.Application.Modules.Identity;
using SaaS.Application.Modules.Memberships;
using SaaS.Application.Modules.PlatformAdmin;
using SaaS.Application.Modules.Roles;
using SaaS.Application.Modules.Subscriptions;
using SaaS.Application.Modules.Tenants;

namespace SaaS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Shared application domain-orchestration services
        services.AddScoped<SubscriptionQuotaService>();
        services.AddScoped<ISubscriptionQuotaService>(sp => sp.GetRequiredService<SubscriptionQuotaService>());
        services.AddScoped<ISubscriptionProvisioner>(sp => sp.GetRequiredService<SubscriptionQuotaService>());

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

        // Memberships & invitations handlers
        services.AddScoped<IQueryHandler<ListMembersQuery, PagedResult<MemberListItemDto>>, ListMembersHandler>();
        services.AddScoped<ICommandHandler<UpdateMemberRoleCommand, MemberListItemDto>, UpdateMemberRoleHandler>();
        services.AddScoped<ICommandHandler<UpdateMemberStatusCommand, MemberListItemDto>, UpdateMemberStatusHandler>();
        services.AddScoped<ICommandHandler<RemoveMemberCommand>, RemoveMemberHandler>();
        services.AddScoped<IQueryHandler<ListInvitationsQuery, PagedResult<InvitationListItemDto>>, ListInvitationsHandler>();
        services.AddScoped<ICommandHandler<InviteMemberCommand, InvitationListItemDto>, InviteMemberHandler>();
        services.AddScoped<ICommandHandler<ResendInvitationCommand, InvitationListItemDto>, ResendInvitationHandler>();
        services.AddScoped<ICommandHandler<RevokeInvitationCommand>, RevokeInvitationHandler>();
        services.AddScoped<ICommandHandler<AcceptInvitationCommand, AcceptInvitationResponse>, AcceptInvitationHandler>();

        // Roles & permissions handlers
        services.AddScoped<IQueryHandler<ListPermissionsQuery, IReadOnlyList<PermissionCatalogItemDto>>, ListPermissionsHandler>();
        services.AddScoped<IQueryHandler<ListRolesQuery, IReadOnlyList<TenantRoleSummaryDto>>, ListRolesHandler>();
        services.AddScoped<ICommandHandler<CreateCustomRoleCommand, TenantRoleSummaryDto>, CreateCustomRoleHandler>();
        services.AddScoped<ICommandHandler<UpdateRoleCommand, TenantRoleSummaryDto>, UpdateRoleHandler>();
        services.AddScoped<ICommandHandler<DeleteRoleCommand>, DeleteRoleHandler>();

        // Subscriptions & usage handlers
        services.AddScoped<IQueryHandler<ListSubscriptionPlansQuery, IReadOnlyList<SubscriptionPlanDto>>, ListSubscriptionPlansHandler>();
        services.AddScoped<IQueryHandler<GetCurrentSubscriptionQuery, CurrentSubscriptionDto>, GetCurrentSubscriptionHandler>();
        services.AddScoped<ICommandHandler<ChangeSubscriptionPlanCommand, CurrentSubscriptionDto>, ChangeSubscriptionPlanHandler>();
        services.AddScoped<ICommandHandler<CancelSubscriptionCommand, CurrentSubscriptionDto>, CancelSubscriptionHandler>();
        services.AddScoped<IQueryHandler<GetSubscriptionHistoryQuery, PagedResult<SubscriptionHistoryDto>>, GetSubscriptionHistoryHandler>();

        // Audit & tenant dashboard handlers
        services.AddScoped<IQueryHandler<GetTenantAuditLogsQuery, PagedResult<AuditLogDto>>, GetTenantAuditLogsHandler>();
        services.AddScoped<IQueryHandler<GetTenantDashboardQuery, TenantDashboardDto>, GetTenantDashboardHandler>();

        // Platform admin handlers
        services.AddScoped<IQueryHandler<GetPlatformDashboardQuery, PlatformDashboardDto>, GetPlatformDashboardHandler>();
        services.AddScoped<IQueryHandler<ListPlatformTenantsQuery, PagedResult<PlatformTenantListItemDto>>, ListPlatformTenantsHandler>();
        services.AddScoped<IQueryHandler<GetPlatformTenantDetailQuery, PlatformTenantDetailDto>, GetPlatformTenantDetailHandler>();
        services.AddScoped<ICommandHandler<SuspendTenantCommand, PlatformTenantDetailDto>, SuspendTenantHandler>();
        services.AddScoped<ICommandHandler<ReactivateTenantCommand, PlatformTenantDetailDto>, ReactivateTenantHandler>();
        services.AddScoped<IQueryHandler<ListPlatformPlansQuery, IReadOnlyList<SubscriptionPlanDto>>, ListPlatformPlansHandler>();
        services.AddScoped<ICommandHandler<CreatePlatformPlanCommand, SubscriptionPlanDto>, CreatePlatformPlanHandler>();
        services.AddScoped<ICommandHandler<UpdatePlatformPlanCommand, SubscriptionPlanDto>, UpdatePlatformPlanHandler>();
        services.AddScoped<IQueryHandler<GetPlatformAuditLogsQuery, PagedResult<AuditLogDto>>, GetPlatformAuditLogsHandler>();

        return services;
    }
}
