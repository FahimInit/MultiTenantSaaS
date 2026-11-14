using SaaS.Domain.Common;
using Shouldly;

namespace SaaS.Domain.UnitTests.Common;

public sealed class EntityAndTenantEntityTests
{
    private sealed class SampleEntity : Entity
    {
        public SampleEntity(Guid id) : base(id) { }
        public void Trigger() => RaiseDomainEvent(new SampleDomainEvent());
    }

    private sealed class OtherEntity : Entity
    {
        public OtherEntity(Guid id) : base(id) { }
    }

    private sealed record SampleDomainEvent : DomainEventBase;

    private sealed class SampleTenantEntity : TenantEntity
    {
        public SampleTenantEntity(Guid tenantId) : base(tenantId) { }
    }

    private sealed class SampleValueObject : ValueObject
    {
        public SampleValueObject(string code, int rank)
        {
            Code = code;
            Rank = rank;
        }

        public string Code { get; }
        public int Rank { get; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Code;
            yield return Rank;
        }
    }

    [Fact]
    public void Entities_WithSameTypeAndId_ShouldBeEqual()
    {
        var id = Guid.CreateVersion7();
        var left = new SampleEntity(id);
        var right = new SampleEntity(id);

        left.ShouldBe(right);
        (left == right).ShouldBeTrue();
    }

    [Fact]
    public void Entities_WithDifferentTypesAndSameId_ShouldNotBeEqual()
    {
        var id = Guid.CreateVersion7();
        Entity left = new SampleEntity(id);
        Entity right = new OtherEntity(id);

        left.ShouldNotBe(right);
    }

    [Fact]
    public void TenantEntity_WithEmptyTenantId_ShouldThrowDomainException()
    {
        Should.Throw<DomainException>(() => new SampleTenantEntity(Guid.Empty));
    }

    [Fact]
    public void Entity_ShouldTrackAndClearDomainEvents()
    {
        var entity = new SampleEntity(Guid.CreateVersion7());
        entity.Trigger();

        entity.DomainEvents.Count.ShouldBe(1);
        entity.ClearDomainEvents();
        entity.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ValueObjects_WithMatchingComponents_ShouldBeEqual()
    {
        var a = new SampleValueObject("PRO", 2);
        var b = new SampleValueObject("PRO", 2);
        var c = new SampleValueObject("FREE", 1);

        a.ShouldBe(b);
        a.ShouldNotBe(c);
    }
}
