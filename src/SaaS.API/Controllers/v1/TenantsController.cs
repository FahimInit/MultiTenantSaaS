using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS.API.Authorization;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Modules.Tenants;
using SaaS.Domain.AccessControl;

namespace SaaS.API.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tenants")]
public sealed class TenantsController : ControllerBase
{
    [Authorize]
    [UserScoped]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserTenantSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMyTenants(
        [FromServices] IQueryHandler<GetMyTenantsQuery, IReadOnlyList<UserTenantSummaryDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetMyTenantsQuery(), cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [UserScoped]
    [HttpPost]
    [ProducesResponseType(typeof(TenantDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTenant(
        [FromBody] CreateTenantRequest request,
        [FromServices] ICommandHandler<CreateTenantCommand, TenantDetailDto> handler,
        CancellationToken cancellationToken)
    {
        var created = await handler.HandleAsync(new CreateTenantCommand(request.Name, request.Slug), cancellationToken);
        return CreatedAtAction(nameof(GetCurrentTenant), null, created);
    }

    [HasPermission(PermissionCatalog.TenantRead)]
    [HttpGet("current")]
    [ProducesResponseType(typeof(TenantDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentTenant(
        [FromServices] IQueryHandler<GetCurrentTenantQuery, TenantDetailDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetCurrentTenantQuery(), cancellationToken);
        return Ok(result);
    }

    [HasPermission(PermissionCatalog.TenantUpdate)]
    [HttpPut("current")]
    [ProducesResponseType(typeof(TenantDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCurrentTenant(
        [FromBody] UpdateTenantRequest request,
        [FromServices] ICommandHandler<UpdateCurrentTenantCommand, TenantDetailDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new UpdateCurrentTenantCommand(request.Name, request.Slug), cancellationToken);
        return Ok(result);
    }

    [HasPermission(PermissionCatalog.TenantUpdate)]
    [HttpPost("current/complete-onboarding")]
    [ProducesResponseType(typeof(TenantDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteOnboarding(
        [FromServices] ICommandHandler<CompleteOnboardingCommand, TenantDetailDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new CompleteOnboardingCommand(), cancellationToken);
        return Ok(result);
    }

    [HasPermission(PermissionCatalog.TenantSettingsRead)]
    [HttpGet("current/settings")]
    [ProducesResponseType(typeof(TenantSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(
        [FromServices] IQueryHandler<GetTenantSettingsQuery, TenantSettingsDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetTenantSettingsQuery(), cancellationToken);
        return Ok(result);
    }

    [HasPermission(PermissionCatalog.TenantSettingsUpdate)]
    [HttpPut("current/settings")]
    [ProducesResponseType(typeof(TenantSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateTenantSettingsRequest request,
        [FromServices] ICommandHandler<UpdateTenantSettingsCommand, TenantSettingsDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new UpdateTenantSettingsCommand(
                request.BillingEmail,
                request.SupportEmail,
                request.Timezone,
                request.Locale,
                request.EnforceEmailDomain,
                request.AllowedEmailDomains,
                request.SessionTimeoutMinutes),
            cancellationToken);
        return Ok(result);
    }
}

public sealed record CreateTenantRequest(string Name, string Slug);
public sealed record UpdateTenantRequest(string Name, string Slug);
public sealed record UpdateTenantSettingsRequest(
    string BillingEmail,
    string? SupportEmail,
    string Timezone,
    string Locale,
    bool EnforceEmailDomain,
    IReadOnlyList<string> AllowedEmailDomains,
    int SessionTimeoutMinutes);
