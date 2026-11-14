using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SaaS.API.Authorization;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Modules.Dashboard;
using SaaS.Domain.AccessControl;

namespace SaaS.API.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dashboard")]
public sealed class DashboardController : ControllerBase
{
    [HasPermission(PermissionCatalog.DashboardRead)]
    [HttpGet]
    [ProducesResponseType(typeof(TenantDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenantDashboard(
        [FromServices] IQueryHandler<GetTenantDashboardQuery, TenantDashboardDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetTenantDashboardQuery(), cancellationToken);
        return Ok(result);
    }
}
