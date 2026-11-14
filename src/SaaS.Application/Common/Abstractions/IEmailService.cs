namespace SaaS.Application.Common.Abstractions;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string email, string fullName, Guid userId, string token, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(string email, string fullName, string token, CancellationToken cancellationToken = default);
    Task SendTenantInvitationAsync(string email, string organizationName, string roleName, string token, CancellationToken cancellationToken = default);
}
