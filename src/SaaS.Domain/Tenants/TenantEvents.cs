using SaaS.Domain.Common;

namespace SaaS.Domain.Tenants;

public sealed record TenantCreatedDomainEvent(Guid TenantId, Guid CreatedByUserId, string Slug) : DomainEventBase;

public sealed record TenantSuspendedDomainEvent(Guid TenantId, string Reason) : DomainEventBase;
