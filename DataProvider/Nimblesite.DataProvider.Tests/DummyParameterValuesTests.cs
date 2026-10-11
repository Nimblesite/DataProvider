namespace Nimblesite.DataProvider.Tests;

// Implements [CON-SHARED-CORE]. Targets DummyParameterValues, the resolver
// the dialect IDatabaseEffects implementations call into when binding
// throwaway parameters before probing a query's column metadata.
public sealed partial class DummyParameterValuesTests
{
    [Theory]
    [MemberData(nameof(Integer_names_return_intInlineCases))]
    public void Integer_names_return_int(string name, Type expected)
    {
        var value = DummyParameterValues.GetDummyValueForParameter(new ParameterInfo(name));
        Assert.NotNull(value);
        Assert.Equal(expected, value.GetType());
    }

    [Theory]
    [MemberData(nameof(Decimal_names_return_decimalInlineCases))]
    public void Decimal_names_return_decimal(string name)
    {
        var value = DummyParameterValues.GetDummyValueForParameter(new ParameterInfo(name));
        Assert.IsType<decimal>(value);
        Assert.Equal(1.0m, (decimal)value);
    }

    [Theory]
    [MemberData(nameof(Temporal_names_return_datetimeInlineCases))]
    public void Temporal_names_return_datetime(string name)
    {
        var value = DummyParameterValues.GetDummyValueForParameter(new ParameterInfo(name));
        Assert.IsType<DateTime>(value);
    }

    [Theory]
    // Id_match_returns_int_one
    [MemberData(nameof(Id_match_returns_int_one_CasesInlineCases))]
    // Limit_match_returns_int_hundred

    // Offset_match_returns_int_zero

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
