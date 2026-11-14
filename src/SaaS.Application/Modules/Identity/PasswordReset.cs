using FluentValidation;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.Audit;

namespace SaaS.Application.Modules.Identity;

public sealed record ForgotPasswordCommand(string Email) : ICommand;

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public sealed class ForgotPasswordHandler : ICommandHandler<ForgotPasswordCommand>
{
    private readonly IValidator<ForgotPasswordCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IEmailService _emailService;
    private readonly IAuditLogger _auditLogger;

    public ForgotPasswordHandler(
        IValidator<ForgotPasswordCommand> validator,
        IIdentityService identityService,
        IEmailService emailService,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _identityService = identityService;
        _emailService = emailService;
        _auditLogger = auditLogger;
    }

    public async Task HandleAsync(ForgotPasswordCommand command, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var user = await _identityService.FindByEmailAsync(command.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || !user.IsActive)
        {
            return;
        }

        var resetToken = await _identityService.GeneratePasswordResetTokenAsync(user.Id, cancellationToken);
        if (!string.IsNullOrEmpty(resetToken))
        {
            await _emailService.SendPasswordResetAsync(user.Email, user.FullName, resetToken, cancellationToken);
        }

        await _auditLogger.LogPlatformAsync(
            user.Id,
            user.Email,
            AuditCategory.Identity,
            "PASSWORD_RESET_REQUESTED",
            "User",
            user.Id.ToString(),
            "Password reset workflow initiated.",
            cancellationToken: cancellationToken);
    }
}

public sealed record ResetPasswordCommand(string Email, string Token, string NewPassword) : ICommand;

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public sealed class ResetPasswordHandler : ICommandHandler<ResetPasswordCommand>
{
    private readonly IValidator<ResetPasswordCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly ISessionStore _sessionStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLogger _auditLogger;

    public ResetPasswordHandler(
        IValidator<ResetPasswordCommand> validator,
        IIdentityService identityService,
        ISessionStore sessionStore,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _identityService = identityService;
        _sessionStore = sessionStore;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _auditLogger = auditLogger;
    }

    public async Task HandleAsync(ResetPasswordCommand command, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var user = await _identityService.FindByEmailAsync(command.Email.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new ValidationAppException(
                new Dictionary<string, string[]> { ["token"] = ["Invalid or expired password reset token."] },
                "Password reset failed.");

        var result = await _identityService.ResetPasswordAsync(user.Id, command.Token, command.NewPassword, cancellationToken);
        if (!result.Succeeded)
        {
            throw new ValidationAppException(
                new Dictionary<string, string[]> { ["token"] = result.Errors.ToArray() },
                "Password reset failed.");
        }

        await _sessionStore.RevokeAllUserSessionsAsync(
            user.Id,
            "Password reset completed",
            _dateTimeProvider.UtcNow,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogPlatformAsync(
            user.Id,
            user.Email,
            AuditCategory.Security,
            "PASSWORD_RESET_COMPLETED",
            "User",
            user.Id.ToString(),
            "User password reset completed and all active sessions revoked.",
            cancellationToken: cancellationToken);
    }
}
