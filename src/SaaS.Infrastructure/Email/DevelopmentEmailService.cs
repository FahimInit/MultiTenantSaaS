using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SaaS.Application.Common.Abstractions;

namespace SaaS.Infrastructure.Email;

public sealed record DispatchedEmailMessage(
    string RecipientEmail,
    string Subject,
    string Token,
    Guid? UserId,
    DateTimeOffset DispatchedAtUtc);

/// <summary>
/// Test-inspection outbox interface registered only in Development/Testing to verify email dispatch workflows.
/// </summary>
public interface IDevEmailOutbox
{
    IReadOnlyCollection<DispatchedEmailMessage> Messages { get; }
    DispatchedEmailMessage? GetLatestFor(string email);
    void Clear();
}

public sealed class DevelopmentEmailService : IEmailService, IDevEmailOutbox
{
    private readonly ConcurrentQueue<DispatchedEmailMessage> _messages = new();
    private readonly ILogger<DevelopmentEmailService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DevelopmentEmailService(
        ILogger<DevelopmentEmailService> logger,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
    }

    public IReadOnlyCollection<DispatchedEmailMessage> Messages => _messages.ToArray();

    public DispatchedEmailMessage? GetLatestFor(string email) =>
        _messages
            .Reverse()
            .FirstOrDefault(m => string.Equals(m.RecipientEmail, email.Trim(), StringComparison.OrdinalIgnoreCase));

    public void Clear() => _messages.Clear();

    public Task SendEmailVerificationAsync(
        string email,
        string fullName,
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(new DispatchedEmailMessage(
            email,
            "Verify your email address",
            token,
            userId,
            _dateTimeProvider.UtcNow));

        _logger.LogInformation(
            "[DEV EMAIL] Email verification dispatched for {Email} (UserId={UserId}).",
            email,
            userId);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(
        string email,
        string fullName,
        string token,
        CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(new DispatchedEmailMessage(
            email,
            "Reset your password",
            token,
            null,
            _dateTimeProvider.UtcNow));

        _logger.LogInformation("[DEV EMAIL] Password reset email dispatched for {Email}.", email);
        return Task.CompletedTask;
    }

    public Task SendTenantInvitationAsync(
        string email,
        string organizationName,
        string roleName,
        string token,
        CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(new DispatchedEmailMessage(
            email,
            $"Invitation to join {organizationName}",
            token,
            null,
            _dateTimeProvider.UtcNow));

        _logger.LogInformation(
            "[DEV EMAIL] Invitation email dispatched for {Email} to join {OrganizationName} as {RoleName}.",
            email,
            organizationName,
            roleName);

        return Task.CompletedTask;
    }
}
