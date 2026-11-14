using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SaaS.API.Authorization;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Models;
using SaaS.Application.Modules.Memberships;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Memberships;

namespace SaaS.API.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/members")]
public sealed class MembersController : ControllerBase
{
    [HasPermission(PermissionCatalog.MembersRead)]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<MemberListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMembers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        [FromQuery] MembershipStatus? status = null,
        [FromQuery] Guid? roleId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string sortDirection = "desc",
        [FromServices] IQueryHandler<ListMembersQuery, PagedResult<MemberListItemDto>> handler = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new ListMembersQuery(page, pageSize, status, roleId, search, sortBy, sortDirection),
            cancellationToken);

        return Ok(result);
    }

    [HasPermission(PermissionCatalog.MembersUpdate)]
    [HttpPut("{memberId:guid}/role")]
    [ProducesResponseType(typeof(MemberListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMemberRole(
        Guid memberId,
        [FromBody] UpdateMemberRoleRequest request,
        [FromServices] ICommandHandler<UpdateMemberRoleCommand, MemberListItemDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new UpdateMemberRoleCommand(memberId, request.RoleId),
            cancellationToken);

        return Ok(result);
    }

    [HasPermission(PermissionCatalog.MembersUpdate)]
    [HttpPut("{memberId:guid}/status")]
    [ProducesResponseType(typeof(MemberListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMemberStatus(
        Guid memberId,
        [FromBody] UpdateMemberStatusRequest request,
        [FromServices] ICommandHandler<UpdateMemberStatusCommand, MemberListItemDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new UpdateMemberStatusCommand(memberId, request.Status),
            cancellationToken);

        return Ok(result);
    }

    [HasPermission(PermissionCatalog.MembersRemove)]
    [HttpDelete("{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMember(
        Guid memberId,
        [FromServices] ICommandHandler<RemoveMemberCommand> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new RemoveMemberCommand(memberId), cancellationToken);
        return NoContent();
    }
}

public sealed record UpdateMemberRoleRequest(Guid RoleId);
public sealed record UpdateMemberStatusRequest(MembershipStatus Status);
