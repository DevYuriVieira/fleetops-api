namespace FleetOps.Domain.Exceptions;

public class InvalidDeliveryStateException : DomainException
{
    public InvalidDeliveryStateException(string message) : base(message)
    {
    }
}
