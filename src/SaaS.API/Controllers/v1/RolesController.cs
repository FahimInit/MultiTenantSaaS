using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SaaS.API.Authorization;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Modules.Roles;
using SaaS.Domain.AccessControl;

namespace SaaS.API.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/roles")]
public sealed class RolesController : ControllerBase
{
    [HasPermission(PermissionCatalog.RolesRead)]
    [HttpGet("/api/v{version:apiVersion}/permissions")]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionCatalogItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPermissions(
        [FromServices] IQueryHandler<ListPermissionsQuery, IReadOnlyList<PermissionCatalogItemDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListPermissionsQuery(), cancellationToken);
        return Ok(result);
    }

    [HasPermission(PermissionCatalog.RolesRead)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TenantRoleSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRoles(
        [FromServices] IQueryHandler<ListRolesQuery, IReadOnlyList<TenantRoleSummaryDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListRolesQuery(), cancellationToken);
        return Ok(result);
    }

    [HasPermission(PermissionCatalog.RolesManage)]
    [HttpPost]
    [ProducesResponseType(typeof(TenantRoleSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRole(
        [FromBody] UpsertRoleRequest request,
        [FromServices] ICommandHandler<CreateCustomRoleCommand, TenantRoleSummaryDto> handler,
        CancellationToken cancellationToken)
    {
        var created = await handler.HandleAsync(
            new CreateCustomRoleCommand(request.Name, request.Description, request.Permissions),
            cancellationToken);

        return Created($"/api/v1/roles/{created.Id}", created);
    }

    [HasPermission(PermissionCatalog.RolesManage)]
    [HttpPut("{roleId:guid}")]
    [ProducesResponseType(typeof(TenantRoleSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateRole(
        Guid roleId,
        [FromBody] UpsertRoleRequest request,
        [FromServices] ICommandHandler<UpdateRoleCommand, TenantRoleSummaryDto> handler,
        CancellationToken cancellationToken)
    {
        var updated = await handler.HandleAsync(
            new UpdateRoleCommand(roleId, request.Name, request.Description, request.Permissions),
            cancellationToken);

        return Ok(updated);
    }

    [HasPermission(PermissionCatalog.RolesManage)]
    [HttpDelete("{roleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteRole(
        Guid roleId,
        [FromServices] ICommandHandler<DeleteRoleCommand> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new DeleteRoleCommand(roleId), cancellationToken);
        return NoContent();
    }
}

public sealed record UpsertRoleRequest(
    string Name,
    string Description,
    IReadOnlyList<string> Permissions);
