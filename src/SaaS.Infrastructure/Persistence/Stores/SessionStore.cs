using Microsoft.EntityFrameworkCore;
using SaaS.Application.Common.Abstractions;
using SaaS.Domain.Identity;

namespace SaaS.Infrastructure.Persistence.Stores;

public sealed class SessionStore : ISessionStore
{
    private readonly ApplicationDbContext _dbContext;

    public SessionStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddSessionAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        await _dbContext.UserSessions.AddAsync(session, cancellationToken);
    }

    public async Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        await _dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);
    }

    public Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public Task<UserSession?> FindSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _dbContext.UserSessions.FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

    public async Task<IReadOnlyList<UserSession>> ListActiveSessionsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbContext.UserSessions
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ToListAsync(cancellationToken);

    public async Task RevokeTokenFamilyAndSessionAsync(
        Guid tokenFamilyId,
        Guid sessionId,
        string reason,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _dbContext.RefreshTokens
            .Where(x => x.TokenFamilyId == tokenFamilyId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(reason, utcNow);
        }

        var session = await _dbContext.UserSessions
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        session?.Revoke(utcNow);
    }

    public async Task RevokeSessionAndTokensAsync(
        Guid sessionId,
        string reason,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.UserSessions
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        session?.Revoke(utcNow);

        var tokens = await _dbContext.RefreshTokens
            .Where(x => x.SessionId == sessionId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(reason, utcNow);
        }
    }

    public async Task RevokeAllUserSessionsAsync(
        Guid userId,
        string reason,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _dbContext.UserSessions
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.Revoke(utcNow);
        }

        var tokens = await _dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(reason, utcNow);
        }
    }
}
