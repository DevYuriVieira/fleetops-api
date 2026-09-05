namespace FleetOps.UnitTests.Domain;

using FleetOps.Domain.Exceptions;
using FleetOps.Domain.ValueObjects;

public class ValueObjectTests
{
    [Fact]
    public void LicensePlate_ShouldNormalizeAndCreate_WhenValid()
    {
        var plate = LicensePlate.Create(" abc-1234 ");

        Assert.Equal("ABC1234", plate.Value);
        Assert.Equal("ABC1234", plate.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    [InlineData("AB")]
    [InlineData("TOOLONGPLATE123456")]
    [InlineData("ABC!123")]
    public void LicensePlate_ShouldThrowDomainValidationException_WhenInvalid(string invalidPlate)
    {
        Assert.Throws<DomainValidationException>(() => LicensePlate.Create(invalidPlate));
    }

    [Fact]
    public void LicensePlate_ShouldImplementValueEquality()
    {
        var plate1 = LicensePlate.Create("ABC1234");
        var plate2 = LicensePlate.Create("abc-1234");

        Assert.Equal(plate1, plate2);
        Assert.True(plate1 == plate2);
    }

    [Fact]
    public void TrackingCode_ShouldNormalizeAndCreate_WhenValid()
    {
        var code = TrackingCode.Create(" trk-123456 ");

        Assert.Equal("TRK-123456", code.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TRK")]
    [InlineData("TRK#12345")]
    public void TrackingCode_ShouldThrowDomainValidationException_WhenInvalid(string invalidCode)
    {
        Assert.Throws<DomainValidationException>(() => TrackingCode.Create(invalidCode));
    }

    [Fact]
    public void TrackingCode_ShouldImplementValueEquality()
    {
        var code1 = TrackingCode.Create("TRK-123456");
        var code2 = TrackingCode.Create("trk-123456");

        Assert.Equal(code1, code2);
    }

    [Fact]
    public void Money_ShouldCreateAndRound_WhenValid()
    {
        var money = new Money(100.556m, "usd");

        Assert.Equal(100.56m, money.Amount);
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void Money_ShouldThrowException_WhenAmountIsNegative()
    {
        Assert.Throws<DomainValidationException>(() => new Money(-1m, "USD"));
    }

    [Fact]
    public void Money_ShouldThrowException_WhenCurrencyIsEmpty()
    {
        Assert.Throws<DomainValidationException>(() => new Money(10m, "  "));
    }

    [Fact]
    public void Money_ShouldAddAndSubtract_WhenSameCurrency()
    {
        var m1 = new Money(50m, "USD");
        var m2 = new Money(20m, "USD");

        var sum = m1 + m2;
        var diff = m1 - m2;

        Assert.Equal(70m, sum.Amount);
        Assert.Equal(30m, diff.Amount);
        Assert.True(m1 > m2);
        Assert.True(m2 < m1);
    }

    [Fact]
    public void Money_ShouldThrowException_WhenOperatingDifferentCurrencies()
    {
        var m1 = new Money(50m, "USD");
        var m2 = new Money(20m, "BRL");

        Assert.Throws<DomainValidationException>(() => m1 + m2);
        Assert.Throws<DomainValidationException>(() => m1 - m2);
    }

    [Fact]
    public void Money_ShouldThrowException_WhenSubtractionBecomesNegative()
    {
        var m1 = new Money(20m, "USD");
        var m2 = new Money(50m, "USD");

        Assert.Throws<DomainValidationException>(() => m1 - m2);
    }

    [Fact]
    public void Address_ShouldCreate_WhenValid()
    {
        var address = new Address(
            "Main Street",
            "100",
            "Downtown",
            "Metropolis",
            "NY",
            "10001",
            "USA",
            "Apt 4B");

        Assert.Equal("Main Street", address.Street);
        Assert.Equal("100", address.Number);
        Assert.Equal("Apt 4B", address.Complement);
        Assert.Equal("USA", address.Country);
    }

    [Fact]
    public void Address_ShouldThrowException_WhenRequiredFieldIsMissing()
    {
        Assert.Throws<DomainValidationException>(() => new Address(
            "",
            "100",
            "Downtown",
            "Metropolis",
            "NY",
            "10001",
            "USA"));
    }

    [Fact]
    public void Address_ShouldImplementValueEquality()
    {
        var addr1 = new Address("Main St", "10", "Center", "City", "ST", "12345", "Country");
        var addr2 = new Address("Main St", "10", "Center", "City", "ST", "12345", "Country");

        Assert.Equal(addr1, addr2);
    }
}
