namespace FleetOps.Domain.ValueObjects;

using FleetOps.Domain.Exceptions;

public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public static Money Zero(string currency = "USD") => new(0m, currency);

    public Money(decimal amount, string currency = "USD")
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new DomainValidationException("Currency code cannot be empty.");
        }

        if (amount < 0m)
        {
            throw new DomainValidationException("Monetary amount cannot be negative.");
        }

        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.Trim().ToUpperInvariant();
    }

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);

        if (left.Amount < right.Amount)
        {
            throw new DomainValidationException("Subtraction results in negative monetary amount.");
        }

        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (!string.Equals(left.Currency, right.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainValidationException($"Cannot perform operations between different currencies: '{left.Currency}' and '{right.Currency}'.");
        }
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
