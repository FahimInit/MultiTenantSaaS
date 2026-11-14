using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Application.Common.Models;
using SaaS.Domain.Audit;

namespace SaaS.Application.Modules.Audit;

public sealed record GetTenantAuditLogsQuery(
    int Page = 1,
    int PageSize = PaginationQuery.DefaultPageSize,
    AuditCategory? Category = null,
    string? Action = null,
    Guid? ActorUserId = null,
    string? Search = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string SortDirection = "desc") : PaginationQuery, IQuery<PagedResult<AuditLogDto>>
{
    public new int Page { get; init; } = Page;
    public new int PageSize { get; init; } = PageSize;
    public new string? Search { get; init; } = Search;
    public new string SortDirection { get; init; } = SortDirection;
}

public sealed class GetTenantAuditLogsHandler
    : IQueryHandler<GetTenantAuditLogsQuery, PagedResult<AuditLogDto>>
{
    private readonly ITenantContext _tenantContext;
    private readonly IAuditQueryStore _auditQueryStore;

    public GetTenantAuditLogsHandler(ITenantContext tenantContext, IAuditQueryStore auditQueryStore)
    {
        _tenantContext = tenantContext;
        _auditQueryStore = auditQueryStore;
    }

    public Task<PagedResult<AuditLogDto>> HandleAsync(
        GetTenantAuditLogsQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAppException();

        return _auditQueryStore.QueryTenantLogsAsync(
            tenantId,
            new TenantAuditFilterQuery(
                query.NormalizedPage,
                query.NormalizedPageSize,
                query.Category,
                query.Action,
                query.ActorUserId,
                query.Search,
                query.FromUtc,
                query.ToUtc,
                query.IsAscending),
            cancellationToken);
    }
}
