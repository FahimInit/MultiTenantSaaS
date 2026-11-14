using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SaaS.API.Authorization;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Models;
using SaaS.Application.Modules.PlatformAdmin;
using SaaS.Domain.Audit;
using SaaS.Domain.Tenants;

namespace SaaS.API.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[PlatformAdminOnly]
[Route("api/v{version:apiVersion}/platform")]
public sealed class PlatformController : ControllerBase
{
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(PlatformDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(
        [FromServices] IQueryHandler<GetPlatformDashboardQuery, PlatformDashboardDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetPlatformDashboardQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("tenants")]
    [ProducesResponseType(typeof(PagedResult<PlatformTenantListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTenants(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        [FromQuery] string? search = null,
        [FromQuery] TenantStatus? status = null,
        [FromQuery] string sortDirection = "desc",
        [FromServices] IQueryHandler<ListPlatformTenantsQuery, PagedResult<PlatformTenantListItemDto>> handler = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new ListPlatformTenantsQuery(page, pageSize, search, status, sortDirection),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("tenants/{tenantId:guid}")]
    [ProducesResponseType(typeof(PlatformTenantDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenantDetail(
        Guid tenantId,
        [FromServices] IQueryHandler<GetPlatformTenantDetailQuery, PlatformTenantDetailDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetPlatformTenantDetailQuery(tenantId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("tenants/{tenantId:guid}/suspend")]
    [ProducesResponseType(typeof(PlatformTenantDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendTenant(
        Guid tenantId,
        [FromBody] SuspendTenantRequest request,
        [FromServices] ICommandHandler<SuspendTenantCommand, PlatformTenantDetailDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new SuspendTenantCommand(tenantId, request.Reason), cancellationToken);
        return Ok(result);
    }

    [HttpPost("tenants/{tenantId:guid}/reactivate")]
    [ProducesResponseType(typeof(PlatformTenantDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReactivateTenant(
        Guid tenantId,
        [FromServices] ICommandHandler<ReactivateTenantCommand, PlatformTenantDetailDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ReactivateTenantCommand(tenantId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("plans")]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPlans(
        [FromServices] IQueryHandler<ListPlatformPlansQuery, IReadOnlyList<SubscriptionPlanDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListPlatformPlansQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("plans")]
    [ProducesResponseType(typeof(SubscriptionPlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePlan(
        [FromBody] CreatePlatformPlanRequest request,
        [FromServices] ICommandHandler<CreatePlatformPlanCommand, SubscriptionPlanDto> handler,
        CancellationToken cancellationToken)
    {
        var created = await handler.HandleAsync(
            new CreatePlatformPlanCommand(
                request.Code,
                request.Name,
                request.Description,
                request.MonthlyPriceCents,
                request.AnnualPriceCents,
                request.TrialDays,
                request.MaxMembers,
                request.MaxCustomRoles,
                request.MaxMonthlyApiCalls,
                request.AuditRetentionDays,
                request.AllowCustomRoles,
                request.IsActive,
                request.IsPublic,
                request.SortOrder),
            cancellationToken);

        return Created($"/api/v1/platform/plans/{created.Id}", created);
    }

    [HttpPut("plans/{planId:guid}")]
    [ProducesResponseType(typeof(SubscriptionPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePlan(
        Guid planId,
        [FromBody] UpdatePlatformPlanRequest request,
        [FromServices] ICommandHandler<UpdatePlatformPlanCommand, SubscriptionPlanDto> handler,
        CancellationToken cancellationToken)
    {
        var updated = await handler.HandleAsync(
            new UpdatePlatformPlanCommand(
                planId,
                request.Name,
                request.Description,
                request.MonthlyPriceCents,
                request.AnnualPriceCents,
                request.TrialDays,
                request.MaxMembers,
                request.MaxCustomRoles,
                request.MaxMonthlyApiCalls,
                request.AuditRetentionDays,
                request.AllowCustomRoles,
                request.IsActive,
                request.IsPublic,
                request.SortOrder),
            cancellationToken);

        return Ok(updated);
    }

    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPlatformAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] AuditScope? scope = null,
        [FromQuery] AuditCategory? category = null,
        [FromQuery] string? action = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTimeOffset? fromUtc = null,
        [FromQuery] DateTimeOffset? toUtc = null,
        [FromQuery] string sortDirection = "desc",
        [FromServices] IQueryHandler<GetPlatformAuditLogsQuery, PagedResult<AuditLogDto>> handler = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new GetPlatformAuditLogsQuery(page, pageSize, tenantId, scope, category, action, search, fromUtc, toUtc, sortDirection),
            cancellationToken);

        return Ok(result);
    }
}

public sealed record SuspendTenantRequest(string Reason);

public sealed record CreatePlatformPlanRequest(
    string Code,
    string Name,
    string Description,
    long MonthlyPriceCents,
    long AnnualPriceCents,
    int TrialDays,
    int? MaxMembers,
    int? MaxCustomRoles,
    int? MaxMonthlyApiCalls,
    int AuditRetentionDays,
    bool AllowCustomRoles,
    bool IsActive,
    bool IsPublic,
    int SortOrder);

public sealed record UpdatePlatformPlanRequest(
    string Name,
    string Description,
    long MonthlyPriceCents,
    long AnnualPriceCents,
    int TrialDays,
    int? MaxMembers,
    int? MaxCustomRoles,
    int? MaxMonthlyApiCalls,
    int AuditRetentionDays,
    bool AllowCustomRoles,
    bool IsActive,
    bool IsPublic,
    int SortOrder);
