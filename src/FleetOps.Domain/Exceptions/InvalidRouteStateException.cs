namespace FleetOps.Domain.Exceptions;

public class InvalidRouteStateException : DomainException
{
    public InvalidRouteStateException(string message) : base(message)
    {
    }
}
