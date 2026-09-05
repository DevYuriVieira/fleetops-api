namespace FleetOps.Application.Exceptions;

public class NotFoundException : ApplicationException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with identifier '{key}' was not found.")
    {
    }

    public NotFoundException(string message) : base(message)
    {
    }
}
