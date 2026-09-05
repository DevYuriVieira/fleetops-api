namespace FleetOps.Domain.ValueObjects;

using System.Text.RegularExpressions;
using FleetOps.Domain.Exceptions;

public sealed partial record TrackingCode
{
    private static readonly Regex CodeRegex = TrackingCodeRegex();

    public string Value { get; }

    private TrackingCode(string value)
    {
        Value = value;
    }

    public static TrackingCode Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Tracking code cannot be empty.");
        }

        var normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length is < 6 or > 32 || !CodeRegex.IsMatch(normalized))
        {
            throw new DomainValidationException($"Invalid tracking code format: '{value}'.");
        }

        return new TrackingCode(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z0-9-]+$")]
    private static partial Regex TrackingCodeRegex();
}
