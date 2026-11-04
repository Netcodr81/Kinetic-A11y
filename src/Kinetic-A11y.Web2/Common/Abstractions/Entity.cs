namespace Kinetic_A11y.Web2.Web.Common.Abstractions;

public abstract class Entity<TId>
    where TId : notnull
{
    protected Entity()
    {
    }

    protected Entity(TId id)
    {
        Id = id;
    }

    public TId Id { get; protected set; } = default!;

  
}
