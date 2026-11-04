namespace Kinetic_A11y.Web2.Web.Common.Abstractions;

public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }
}
