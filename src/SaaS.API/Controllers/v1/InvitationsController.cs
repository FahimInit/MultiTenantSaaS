using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS.API.Authorization;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Models;
using SaaS.Application.Modules.Memberships;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Invitations;

namespace SaaS.API.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/invitations")]
public sealed class InvitationsController : ControllerBase
{
    [HasPermission(PermissionCatalog.MembersRead)]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<InvitationListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListInvitations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        [FromQuery] InvitationStatus? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string sortDirection = "desc",
        [FromServices] IQueryHandler<ListInvitationsQuery, PagedResult<InvitationListItemDto>> handler = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new ListInvitationsQuery(page, pageSize, status, search, sortDirection),
            cancellationToken);

        return Ok(result);
    }

    [HasPermission(PermissionCatalog.MembersInvite)]
    [HttpPost]
    [ProducesResponseType(typeof(InvitationListItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InviteMember(
        [FromBody] InviteMemberRequest request,
        [FromServices] ICommandHandler<InviteMemberCommand, InvitationListItemDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new InviteMemberCommand(request.Email, request.RoleId),
            cancellationToken);

        return Created($"/api/v1/invitations/{result.Id}", result);
    }

    [HasPermission(PermissionCatalog.MembersInvite)]
    [HttpPost("{invitationId:guid}/resend")]
    [ProducesResponseType(typeof(InvitationListItemDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResendInvitation(
        Guid invitationId,
        [FromServices] ICommandHandler<ResendInvitationCommand, InvitationListItemDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ResendInvitationCommand(invitationId), cancellationToken);
        return Accepted(result);
    }

    [HasPermission(PermissionCatalog.MembersInvite)]
    [HttpDelete("{invitationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeInvitation(
        Guid invitationId,
        [FromServices] ICommandHandler<RevokeInvitationCommand> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new RevokeInvitationCommand(invitationId), cancellationToken);
        return NoContent();
    }

    [Authorize]
    [UserScoped]
    [HttpPost("accept")]
    [ProducesResponseType(typeof(AcceptInvitationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptInvitation(
        [FromBody] AcceptInvitationRequest request,
        [FromServices] ICommandHandler<AcceptInvitationCommand, AcceptInvitationResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new AcceptInvitationCommand(request.Token), cancellationToken);
        return Ok(result);
    }
}

public sealed record InviteMemberRequest(string Email, Guid RoleId);
public sealed record AcceptInvitationRequest(string Token);
