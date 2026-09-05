namespace FleetOps.Domain.Exceptions;

public class InvalidDriverStateException : DomainException
{
    public InvalidDriverStateException(string message) : base(message)
    {
    }
}
