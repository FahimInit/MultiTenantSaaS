using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SaaS.API.Authorization;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Models;
using SaaS.Application.Modules.Audit;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Audit;

namespace SaaS.API.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audit-logs")]
public sealed class AuditLogsController : ControllerBase
{
    [HasPermission(PermissionCatalog.AuditRead)]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        [FromQuery] AuditCategory? category = null,
        [FromQuery] string? action = null,
        [FromQuery] Guid? actorUserId = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTimeOffset? fromUtc = null,
        [FromQuery] DateTimeOffset? toUtc = null,
        [FromQuery] string sortDirection = "desc",
        [FromServices] IQueryHandler<GetTenantAuditLogsQuery, PagedResult<AuditLogDto>> handler = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new GetTenantAuditLogsQuery(page, pageSize, category, action, actorUserId, search, fromUtc, toUtc, sortDirection),
            cancellationToken);

        return Ok(result);
    }
}
