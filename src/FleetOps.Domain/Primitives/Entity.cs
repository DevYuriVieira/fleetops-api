namespace FleetOps.Domain.Primitives;

using FleetOps.Domain.Exceptions;

public abstract class Entity : IEquatable<Entity>
{
    public Guid Id { get; protected init; }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Entity identifier cannot be empty.");
        }

        Id = id;
    }

    protected Entity()
    {
    }

    public override bool Equals(object? obj)
    {
        return obj is Entity entity && Equals(entity);
    }

    public bool Equals(Entity? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        return Id != Guid.Empty && Id == other.Id;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GetType(), Id);
    }

    public static bool operator ==(Entity? left, Entity? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Entity? left, Entity? right)
    {
        return !Equals(left, right);
    }
}
