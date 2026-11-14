using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;

namespace SaaS.Application.Modules.Dashboard;

public sealed record GetTenantDashboardQuery : IQuery<TenantDashboardDto>;

public sealed class GetTenantDashboardHandler : IQueryHandler<GetTenantDashboardQuery, TenantDashboardDto>
{
    private readonly ITenantContext _tenantContext;
    private readonly IDashboardStore _dashboardStore;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetTenantDashboardHandler(
        ITenantContext tenantContext,
        IDashboardStore dashboardStore,
        IDateTimeProvider dateTimeProvider)
    {
        _tenantContext = tenantContext;
        _dashboardStore = dashboardStore;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<TenantDashboardDto> HandleAsync(
        GetTenantDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();

        return await _dashboardStore.GetTenantDashboardAsync(tenantId, _dateTimeProvider.UtcNow, cancellationToken)
            ?? throw new NotFoundAppException("Organization dashboard data was not found.", "tenant_not_found");
    }
}
