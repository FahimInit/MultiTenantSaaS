using FluentValidation;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.Audit;
using SaaS.Domain.Tenants;

namespace SaaS.Application.Modules.Tenants;

public sealed record GetTenantSettingsQuery : IQuery<TenantSettingsDto>;

public sealed class GetTenantSettingsHandler : IQueryHandler<GetTenantSettingsQuery, TenantSettingsDto>
{
    private readonly ITenantContext _tenantContext;
    private readonly ITenantStore _tenantStore;

    public GetTenantSettingsHandler(ITenantContext tenantContext, ITenantStore tenantStore)
    {
        _tenantContext = tenantContext;
        _tenantStore = tenantStore;
    }

    public async Task<TenantSettingsDto> HandleAsync(
        GetTenantSettingsQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new ForbiddenAppException("Tenant context is not resolved.", "tenant_context_missing");

        var settings = await _tenantStore.GetCurrentTenantSettingsAsync(tenantId, cancellationToken)
            ?? throw new NotFoundAppException("Organization settings were not found.", "tenant_settings_not_found");

        return new TenantSettingsDto(
            settings.TenantId,
            settings.BillingEmail,
            settings.SupportEmail,
            settings.Timezone,
            settings.Locale,
            settings.EnforceEmailDomain,
            settings.AllowedEmailDomains,
            settings.SessionTimeoutMinutes,
            settings.UpdatedAtUtc);
    }
}

public sealed record UpdateTenantSettingsCommand(
    string BillingEmail,
    string? SupportEmail,
    string Timezone,
    string Locale,
    bool EnforceEmailDomain,
    IReadOnlyList<string> AllowedEmailDomains,
    int SessionTimeoutMinutes) : ICommand<TenantSettingsDto>;

public sealed class UpdateTenantSettingsValidator : AbstractValidator<UpdateTenantSettingsCommand>
{
    public UpdateTenantSettingsValidator()
    {
        RuleFor(x => x.BillingEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.SupportEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.SupportEmail));
        RuleFor(x => x.Timezone).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Locale).NotEmpty().MaximumLength(24);
        RuleFor(x => x.SessionTimeoutMinutes)
            .InclusiveBetween(TenantSettings.MinSessionTimeoutMinutes, TenantSettings.MaxSessionTimeoutMinutes);
    }
}

public sealed class UpdateTenantSettingsHandler : ICommandHandler<UpdateTenantSettingsCommand, TenantSettingsDto>
{
    private readonly IValidator<UpdateTenantSettingsCommand> _validator;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantStore _tenantStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public UpdateTenantSettingsHandler(
        IValidator<UpdateTenantSettingsCommand> validator,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        ITenantStore tenantStore,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _tenantStore = tenantStore;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task<TenantSettingsDto> HandleAsync(
        UpdateTenantSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var tenantId = _tenantContext.TenantId
            ?? throw new ForbiddenAppException("Tenant context is not resolved.", "tenant_context_missing");

        var settings = await _tenantStore.GetCurrentTenantSettingsAsync(tenantId, cancellationToken)
            ?? throw new NotFoundAppException("Organization settings were not found.", "tenant_settings_not_found");

        var now = _dateTimeProvider.UtcNow;
        settings.Update(
            command.BillingEmail,
            command.SupportEmail,
            command.Timezone,
            command.Locale,
            command.EnforceEmailDomain,
            command.AllowedEmailDomains,
            command.SessionTimeoutMinutes,
            now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogTenantAsync(
            tenantId,
            _currentUser.UserId,
            _currentUser.Email ?? "unknown",
            AuditCategory.Tenant,
            "TENANT_SETTINGS_UPDATED",
            "TenantSettings",
            settings.Id.ToString(),
            "Organization settings updated.",
            new Dictionary<string, object?>
            {
                ["enforceEmailDomain"] = settings.EnforceEmailDomain,
                ["sessionTimeoutMinutes"] = settings.SessionTimeoutMinutes
            },
            cancellationToken);

        return new TenantSettingsDto(
            settings.TenantId,
            settings.BillingEmail,
            settings.SupportEmail,
            settings.Timezone,
            settings.Locale,
            settings.EnforceEmailDomain,
            settings.AllowedEmailDomains,
            settings.SessionTimeoutMinutes,
            settings.UpdatedAtUtc);
    }
}
