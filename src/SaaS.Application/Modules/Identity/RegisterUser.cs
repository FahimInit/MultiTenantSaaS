using FluentValidation;
using SaaS.Application.Common.Abstractions;
using SaaS.Application.Common.Exceptions;
using SaaS.Domain.Audit;

namespace SaaS.Application.Modules.Identity;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FullName) : ICommand<RegisterUserResponse>;

public sealed class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(200);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);
    }
}

public sealed class RegisterUserHandler : ICommandHandler<RegisterUserCommand, RegisterUserResponse>
{
    private readonly IValidator<RegisterUserCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IEmailService _emailService;
    private readonly IAuditLogger _auditLogger;

    public RegisterUserHandler(
        IValidator<RegisterUserCommand> validator,
        IIdentityService identityService,
        IEmailService emailService,
        IAuditLogger auditLogger)
    {
        _validator = validator;
        _identityService = identityService;
        _emailService = emailService;
        _auditLogger = auditLogger;
    }

    public async Task<RegisterUserResponse> HandleAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var existing = await _identityService.FindByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictAppException(
                "An account with this email address already exists.",
                "email_already_registered");
        }

        var (result, user) = await _identityService.CreateUserAsync(
            normalizedEmail,
            command.Password,
            command.FullName.Trim(),
            emailConfirmed: false,
            cancellationToken);

        if (!result.Succeeded || user is null)
        {
            throw new ValidationAppException(
                new Dictionary<string, string[]> { ["password"] = result.Errors.ToArray() },
                "User registration failed.");
        }

        var verificationToken = await _identityService.GenerateEmailVerificationTokenAsync(user.Id, cancellationToken);
        if (!string.IsNullOrEmpty(verificationToken))
        {
            await _emailService.SendEmailVerificationAsync(
                user.Email,
                user.FullName,
                user.Id,
                verificationToken,
                cancellationToken);
        }

        await _auditLogger.LogPlatformAsync(
            user.Id,
            user.Email,
            AuditCategory.Identity,
            "USER_REGISTERED",
            "User",
            user.Id.ToString(),
            "User account registered and email verification dispatched.",
            cancellationToken: cancellationToken);

        return new RegisterUserResponse(user.Id, user.Email, EmailVerificationRequired: true);
    }
}
