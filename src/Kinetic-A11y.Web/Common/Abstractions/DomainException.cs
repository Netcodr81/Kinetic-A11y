namespace Kinetic_A11y.Web.Common.Abstractions;

public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }
}
