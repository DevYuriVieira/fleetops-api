namespace FleetOps.Domain.Exceptions;

public class InvalidMaintenanceStateException : DomainException
{
    public InvalidMaintenanceStateException(string message) : base(message)
    {
    }
}
