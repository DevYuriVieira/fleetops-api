namespace FleetOps.Infrastructure.Persistence;

using FleetOps.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public static class DatabaseExceptionMapper
{
    public static Exception Map(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return new ConflictException("The record was modified or deleted by another concurrent transaction.");
        }

        if (exception is DbUpdateException dbUpdateException && dbUpdateException.InnerException is PostgresException pgEx)
        {
            return MapPostgresException(pgEx, dbUpdateException);
        }

        if (exception is PostgresException postgresException)
        {
            return MapPostgresException(postgresException, postgresException);
        }

        return exception;
    }

    private static Exception MapPostgresException(PostgresException pgEx, Exception originalException)
    {
        // PostgreSQL Error Codes:
        // 23505 = unique_violation
        // 23503 = foreign_key_violation
        switch (pgEx.SqlState)
        {
            case PostgresErrorCodes.UniqueViolation:
                return pgEx.ConstraintName switch
                {
                    "uq_maintenances_vehicle_active" or "ix_maintenances_vehicle_id" =>
                        new ConflictException("Vehicle already has an active maintenance record."),
                    "uq_vehicles_license_plate" =>
                        new ConflictException("A vehicle with the specified license plate already exists."),
                    "uq_drivers_license_number" =>
                        new ConflictException("A driver with the specified license number already exists."),
                    "uq_deliveries_tracking_code" =>
                        new ConflictException("A delivery with the specified tracking code already exists."),
                    _ =>
                        new ConflictException($"A unique constraint violation occurred: {pgEx.ConstraintName ?? pgEx.MessageText}")
                };

            case PostgresErrorCodes.ForeignKeyViolation:
                return new ConflictException($"A foreign key constraint was violated: {pgEx.ConstraintName ?? pgEx.MessageText}");

            default:
                return originalException;
        }
    }
}
