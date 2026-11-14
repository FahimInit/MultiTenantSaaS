using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaS.API.Authorization;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Models;
using SaaS.Application.Modules.Subscriptions;
using SaaS.Domain.AccessControl;
using SaaS.Domain.Subscriptions;

namespace SaaS.API.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/subscriptions")]
public sealed class SubscriptionsController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("plans")]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPublicPlans(
        [FromServices] IQueryHandler<ListSubscriptionPlansQuery, IReadOnlyList<SubscriptionPlanDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListSubscriptionPlansQuery(), cancellationToken);
        return Ok(result);
    }

    [HasPermission(PermissionCatalog.BillingRead)]
    [HttpGet("current")]
    [ProducesResponseType(typeof(CurrentSubscriptionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentSubscription(
        [FromServices] IQueryHandler<GetCurrentSubscriptionQuery, CurrentSubscriptionDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetCurrentSubscriptionQuery(), cancellationToken);
        return Ok(result);
    }

    [HasPermission(PermissionCatalog.BillingManage)]
    [HttpPost("change-plan")]
    [ProducesResponseType(typeof(CurrentSubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePlan(
        [FromBody] ChangeSubscriptionPlanRequest request,
        [FromServices] ICommandHandler<ChangeSubscriptionPlanCommand, CurrentSubscriptionDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ChangeSubscriptionPlanCommand(request.PlanCode, request.BillingCycle, request.PaymentMethodToken),
            cancellationToken);

        return Ok(result);
    }

    [HasPermission(PermissionCatalog.BillingManage)]
    [HttpPost("cancel")]
    [ProducesResponseType(typeof(CurrentSubscriptionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelSubscription(
        [FromBody] CancelSubscriptionRequest? request,
        [FromServices] ICommandHandler<CancelSubscriptionCommand, CurrentSubscriptionDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new CancelSubscriptionCommand(request?.Reason), cancellationToken);
        return Ok(result);
    }

    [HasPermission(PermissionCatalog.BillingRead)]
    [HttpGet("history")]
    [ProducesResponseType(typeof(PagedResult<SubscriptionHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        [FromServices] IQueryHandler<GetSubscriptionHistoryQuery, PagedResult<SubscriptionHistoryDto>> handler = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(new GetSubscriptionHistoryQuery(page, pageSize), cancellationToken);
        return Ok(result);
    }
}

public sealed record ChangeSubscriptionPlanRequest(
    string PlanCode,
    BillingCycle BillingCycle,
    string? PaymentMethodToken = null);

public sealed record CancelSubscriptionRequest(string? Reason = null);
