namespace FleetOps.Domain.ValueObjects;

using FleetOps.Domain.Exceptions;

public sealed record Address
{
    public string Street { get; }
    public string Number { get; }
    public string? Complement { get; }
    public string Neighborhood { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string Country { get; }

    public Address(
        string street,
        string number,
        string neighborhood,
        string city,
        string state,
        string postalCode,
        string country,
        string? complement = null)
    {
        if (string.IsNullOrWhiteSpace(street))
        {
            throw new DomainValidationException("Street cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(number))
        {
            throw new DomainValidationException("Number cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(neighborhood))
        {
            throw new DomainValidationException("Neighborhood cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new DomainValidationException("City cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            throw new DomainValidationException("State cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            throw new DomainValidationException("Postal code cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            throw new DomainValidationException("Country cannot be empty.");
        }

        Street = street.Trim();
        Number = number.Trim();
        Neighborhood = neighborhood.Trim();
        City = city.Trim();
        State = state.Trim();
        PostalCode = postalCode.Trim();
        Country = country.Trim();
        Complement = string.IsNullOrWhiteSpace(complement) ? null : complement.Trim();
    }

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Complement)
            ? $"{Street}, {Number} - {Neighborhood}, {City} - {State}, {PostalCode}, {Country}"
            : $"{Street}, {Number} ({Complement}) - {Neighborhood}, {City} - {State}, {PostalCode}, {Country}";
    }
}
