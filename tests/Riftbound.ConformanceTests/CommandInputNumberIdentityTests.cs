using System.Reflection;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CommandInputNumberIdentityTests
{
    [Theory]
    [InlineData("1.230e-5", "0.00001230")]
    [InlineData("1e3", "1000")]
    [InlineData("1E+0003", "1000.000")]
    [InlineData("-0.0000", "0")]
    [InlineData("0e999999999999999999999", "-0")]
    [InlineData("100000000000000000000000000001.00", "100000000000000000000000000001")]
    [InlineData("9e999999999999999999999", "90e999999999999999999998")]
    [InlineData("1e-999999999999999999999", "10e-1000000000000000000000")]
    [InlineData("[1e3,-0]", "[1000,0.000]")]
    [InlineData("{\"b\":1e1,\"a\":2}", "{\"a\":2.0,\"b\":10}")]
    public void EquivalentJsonValuesHaveOneInputIdentityWithoutExpandingLargeExponents(string first, string second)
        => Assert.Equal(Fingerprint(first), Fingerprint(second));

    [Theory]
    [InlineData("100000000000000000000000000001", "100000000000000000000000000002")]
    [InlineData("1.00000000000000000000000000001", "1")]
    [InlineData("1e9999", "1e10000")]
    [InlineData("0", "\"0\"")]
    [InlineData("[1,2]", "[2,1]")]
    [InlineData("{\"a\":1}", "{\"a\":2}")]
    public void DifferentValuesTypesAndArrayOrderCannotAlias(string first, string second)
        => Assert.NotEqual(Fingerprint(first), Fingerprint(second));

    [Fact]
    public void StateAndHistoryHashingRetainsItsExistingNumericRepresentation()
    {
        var exponent = Wire("1.230e-5");
        var decimalForm = Wire("0.00001230");
        Assert.NotEqual(MatchStateHasher.HashValue(exponent), MatchStateHasher.HashValue(decimalForm));
        Assert.Equal(Fingerprint("1.230e-5"), Fingerprint("0.00001230"));
    }

    private static JsonElement Wire(string value) => JsonDocument.Parse(
        "{\"cmdType\":\"MULLIGAN\",\"handObjectIds\":[],\"trace\":" + value + "}").RootElement.Clone();

    private static string Fingerprint(string value) => Assert.IsType<string>(typeof(MatchSession)
        .GetMethod("InputFingerprint", BindingFlags.NonPublic | BindingFlags.Static)!
        .Invoke(null, [Wire(value), TypedCommandInput.Capture(new MulliganCommand([]))]));
}
