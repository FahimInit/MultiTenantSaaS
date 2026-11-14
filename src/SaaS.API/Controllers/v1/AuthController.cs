using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SaaS.API.Authorization;
using SaaS.API.Extensions;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Modules.Identity;

namespace SaaS.API.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public sealed class AuthController : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicyName)]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        [FromServices] ICommandHandler<RegisterUserCommand, RegisterUserResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RegisterUserCommand(request.Email, request.Password, request.FullName),
            cancellationToken);
        return Created(string.Empty, result);
    }

    [AllowAnonymous]
    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailRequest request,
        [FromServices] ICommandHandler<VerifyEmailCommand> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new VerifyEmailCommand(request.UserId, request.Token), cancellationToken);
        return Ok(new { verified = true });
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicyName)]
    [HttpPost("resend-verification")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendVerification(
        [FromBody] EmailOnlyRequest request,
        [FromServices] ICommandHandler<ResendVerificationCommand> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new ResendVerificationCommand(request.Email), cancellationToken);
        return Accepted(new { message = "If an unverified account exists, a verification email has been dispatched." });
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicyName)]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] ICommandHandler<LoginCommand, AuthTokenResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new LoginCommand(request.Email, request.Password), cancellationToken);
        return Ok(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicyName)]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        [FromServices] ICommandHandler<RefreshTokenCommand, AuthTokenResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new RefreshTokenCommand(request.RefreshToken), cancellationToken);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequest request,
        [FromServices] ICommandHandler<LogoutCommand> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new LogoutCommand(request.RefreshToken), cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicyName)]
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] EmailOnlyRequest request,
        [FromServices] ICommandHandler<ForgotPasswordCommand> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new ForgotPasswordCommand(request.Email), cancellationToken);
        return Accepted(new { message = "If an account exists for this email, password reset instructions have been sent." });
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingExtensions.AuthPolicyName)]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        [FromServices] ICommandHandler<ResetPasswordCommand> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(
            new ResetPasswordCommand(request.Email, request.Token, request.NewPassword),
            cancellationToken);
        return Ok(new { reset = true });
    }

    [Authorize]
    [UserScoped]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(
        [FromServices] IQueryHandler<GetCurrentUserQuery, CurrentUserResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetCurrentUserQuery(), cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [UserScoped]
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(IReadOnlyList<UserSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions(
        [FromServices] IQueryHandler<GetActiveSessionsQuery, IReadOnlyList<UserSessionDto>> handler,
        CancellationToken cancellationToken)
    {
        var sessions = await handler.HandleAsync(new GetActiveSessionsQuery(), cancellationToken);
        return Ok(sessions);
    }

    [Authorize]
    [UserScoped]
    [HttpDelete("sessions/{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSession(
        Guid sessionId,
        [FromServices] ICommandHandler<RevokeSessionCommand> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new RevokeSessionCommand(sessionId), cancellationToken);
        return NoContent();
    }
}

public sealed record RegisterRequest(string Email, string Password, string FullName);
public sealed record VerifyEmailRequest(Guid UserId, string Token);
public sealed record EmailOnlyRequest(string Email);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
