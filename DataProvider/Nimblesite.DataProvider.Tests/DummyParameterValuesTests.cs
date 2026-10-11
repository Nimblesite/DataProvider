using Nimblesite.Sql.Model;

namespace Nimblesite.DataProvider.Tests;

// Implements [CON-SHARED-CORE]. Targets DummyParameterValues, the resolver
// the dialect IDatabaseEffects implementations call into when binding
// throwaway parameters before probing a query's column metadata.
public sealed class DummyParameterValuesTests
{
    [Theory]
    [InlineData("userId", typeof(int))]
    [InlineData("id", typeof(int))]
    [InlineData("USER_ID", typeof(int))]
    [InlineData("limit", typeof(int))]
    [InlineData("LIMIT", typeof(int))]
    [InlineData("offset", typeof(int))]
    [InlineData("count", typeof(int))]
    [InlineData("row_count", typeof(int))]
    [InlineData("quantity", typeof(int))]
    public void Integer_names_return_int(string name, Type expected)
    {
        var value = DummyParameterValues.GetDummyValueForParameter(new ParameterInfo(name));
        Assert.NotNull(value);
        Assert.Equal(expected, value.GetType());
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("price")]
    [InlineData("total")]
    [InlineData("percentage")]
    [InlineData("unit_price")]
    public void Decimal_names_return_decimal(string name)
    {
        var value = DummyParameterValues.GetDummyValueForParameter(new ParameterInfo(name));
        Assert.IsType<decimal>(value);
        Assert.Equal(1.0m, (decimal)value);
    }

    [Theory]
    [InlineData("date")]
    [InlineData("startDate")]
    [InlineData("time")]
    [InlineData("created")]
    [InlineData("updated")]
    public void Temporal_names_return_datetime(string name)
    {
        var value = DummyParameterValues.GetDummyValueForParameter(new ParameterInfo(name));
        Assert.IsType<DateTime>(value);
    }

    [Theory]
    // Id_match_returns_int_one
    [InlineData("id", 1)]
    // Limit_match_returns_int_hundred
    [InlineData("limit", 100)]
    // Offset_match_returns_int_zero
    [InlineData("offset", 0)]
    public void Id_match_returns_int_one_Cases(string caseValue0, int caseValue1)
    {
        var value = DummyParameterValues.GetDummyValueForParameter(new ParameterInfo(caseValue0));
        Assert.Equal(caseValue1, value);
    }

    [Fact]
    public void Unknown_name_returns_dummy_string()
    {
        var value = DummyParameterValues.GetDummyValueForParameter(new ParameterInfo("foo"));
        Assert.Equal("dummy_value", value);
    }
}
