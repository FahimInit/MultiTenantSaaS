using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaaS.Application.Common.Abstractions;

namespace SaaS.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; init; } = "Development";
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1025;
    public bool EnableSsl { get; init; }
    public string FromAddress { get; init; } = "no-reply@saas.local";
    public string FromName { get; init; } = "Multi-Tenant SaaS Platform";
    public string? Username { get; init; }
    public string? Password { get; init; }
}

public sealed class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task SendEmailVerificationAsync(
        string email,
        string fullName,
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var body = $"""
            Hello {fullName},

            Please verify your email address using User ID '{userId}' and verification token:
            {token}
            """;

        return SendAsync(email, "Verify your email address", body, cancellationToken);
    }

    public Task SendPasswordResetAsync(
        string email,
        string fullName,
        string token,
        CancellationToken cancellationToken = default)
    {
        var body = $"""
            Hello {fullName},

            A password reset was requested for your account. Use token:
            {token}
            """;

        return SendAsync(email, "Reset your password", body, cancellationToken);
    }

    public Task SendTenantInvitationAsync(
        string email,
        string organizationName,
        string roleName,
        string token,
        CancellationToken cancellationToken = default)
    {
        var body = $"""
            You have been invited to join '{organizationName}' as {roleName}.

            Accept this invitation using token:
            {token}
            """;

        return SendAsync(email, $"Invitation to join {organizationName}", body, cancellationToken);
    }

    private async Task SendAsync(
        string recipientEmail,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(recipientEmail));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
        _logger.LogInformation("SMTP email '{Subject}' dispatched to {RecipientEmail}.", subject, recipientEmail);
    }
}
