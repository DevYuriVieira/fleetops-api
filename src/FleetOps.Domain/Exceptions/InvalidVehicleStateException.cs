namespace FleetOps.Domain.Exceptions;

public class InvalidVehicleStateException : DomainException
{
    public InvalidVehicleStateException(string message) : base(message)
    {
    }
}
