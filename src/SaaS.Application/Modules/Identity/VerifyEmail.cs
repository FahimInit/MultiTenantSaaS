using FluentValidation;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.Audit;

namespace SaaS.Application.Modules.Identity;

public sealed record VerifyEmailCommand(Guid UserId, string Token) : ICommand;

public sealed class VerifyEmailValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty();
    }
}

public sealed class VerifyEmailHandler : ICommandHandler<VerifyEmailCommand>
{
    private readonly IValidator<VerifyEmailCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IAuditLogger _auditLogger;

    public VerifyEmailHandler(
        IValidator<VerifyEmailCommand> validator,
        IIdentityService identityService,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _identityService = identityService;
        _auditLogger = auditLogger;
    }

    public async Task HandleAsync(VerifyEmailCommand command, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var user = await _identityService.FindByIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundAppException("User account was not found.", "user_not_found");

        if (user.EmailConfirmed)
        {
            return;
        }

        var result = await _identityService.ConfirmEmailAsync(command.UserId, command.Token, cancellationToken);
        if (!result.Succeeded)
        {
            throw new ValidationAppException(
                new Dictionary<string, string[]> { ["token"] = result.Errors.ToArray() },
                "Invalid or expired email verification token.");
        }

        await _auditLogger.LogPlatformAsync(
            user.Id,
            user.Email,
            AuditCategory.Identity,
            "EMAIL_VERIFIED",
            "User",
            user.Id.ToString(),
            "User email address verified.",
            cancellationToken: cancellationToken);
    }
}

public sealed record ResendVerificationCommand(string Email) : ICommand;

public sealed class ResendVerificationValidator : AbstractValidator<ResendVerificationCommand>
{
    public ResendVerificationValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public sealed class ResendVerificationHandler : ICommandHandler<ResendVerificationCommand>
{
    private readonly IValidator<ResendVerificationCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IEmailService _emailService;

    public ResendVerificationHandler(
        IValidator<ResendVerificationCommand> validator,
        IIdentityService identityService,
        IEmailService emailService)
    {
        _validator = validator;
        _identityService = identityService;
        _emailService = emailService;
    }

    public async Task HandleAsync(ResendVerificationCommand command, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var user = await _identityService.FindByEmailAsync(command.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || user.EmailConfirmed || !user.IsActive)
        {
            // Non-enumerating response: avoid leaking account existence.
            return;
        }

        var token = await _identityService.GenerateEmailVerificationTokenAsync(user.Id, cancellationToken);
        if (!string.IsNullOrEmpty(token))
        {
            await _emailService.SendEmailVerificationAsync(
                user.Email,
                user.FullName,
                user.Id,
                token,
                cancellationToken);
        }
    }
}
