namespace FleetOps.Domain.ValueObjects;

using System.Text.RegularExpressions;
using FleetOps.Domain.Exceptions;

public sealed partial record LicensePlate
{
    private static readonly Regex PlateRegex = LicensePlateRegex();

    public string Value { get; }

    private LicensePlate(string value)
    {
        Value = value;
    }

    public static LicensePlate Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("License plate cannot be empty.");
        }

        var normalized = value.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");

        if (normalized.Length is < 3 or > 10 || !PlateRegex.IsMatch(normalized))
        {
            throw new DomainValidationException($"Invalid license plate format: '{value}'.");
        }

        return new LicensePlate(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z0-9]+$")]
    private static partial Regex LicensePlateRegex();
}
