using System.Collections;
using System.Data;

#pragma warning disable CS8765 // Nullability of parameter doesn't match overridden member

namespace Nimblesite.DataProvider.Tests.Fakes;

/// <summary>
/// Fake database parameter for testing
/// </summary>
internal sealed class FakeParameter : DbParameter
{
    public override DbType DbType { get; set; } = DbType.String;

    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

    public override bool IsNullable { get; set; } = true;

    public override string ParameterName { get; set; } = string.Empty;

    public override int Size { get; set; }

    public override string SourceColumn { get; set; } = string.Empty;

    public override bool SourceColumnNullMapping { get; set; }

    public override object? Value { get; set; }

    public override void ResetDbType() => DbType = DbType.String;
}

/// <summary>
/// Fake database parameter collection for testing
/// </summary>
internal sealed class FakeParameterCollection : DbParameterCollection
{
    private readonly List<FakeParameter> _parameters = [];

    public override int Count => _parameters.Count;

    public override object SyncRoot => _parameters;

    public override bool IsFixedSize => false;

    public override bool IsReadOnly => false;

    public override bool IsSynchronized => false;

    public override int Add(object value)
    {
        if (value is FakeParameter parameter)
        {
            _parameters.Add(parameter);
            return _parameters.Count - 1;
        }
        throw new ArgumentException("Value must be a FakeParameter", nameof(value));
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
        {
            Add(value);
        }
    }

    public override void Clear() => _parameters.Clear();

    public override bool Contains(object value) => _parameters.Contains(value);

    public override bool Contains(string value) => _parameters.Any(p => p.ParameterName == value);

    public override void CopyTo(Array array, int index) =>
        ((ICollection)_parameters).CopyTo(array, index);

    public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();

    public override int IndexOf(object value) => _parameters.IndexOf((FakeParameter)value);

    public override int IndexOf(string parameterName) =>
        _parameters.FindIndex(p => p.ParameterName == parameterName);

    public override void Insert(int index, object value) =>
        _parameters.Insert(index, (FakeParameter)value);

    public override void Remove(object value) => _parameters.Remove((FakeParameter)value);

    public override void RemoveAt(int index) => _parameters.RemoveAt(index);

    public override void RemoveAt(string parameterName)
    {
        var index = IndexOf(parameterName);
        if (index >= 0)
            RemoveAt(index);
    }

    protected override DbParameter GetParameter(int index) => _parameters[index];

    protected override DbParameter GetParameter(string parameterName) =>
        _parameters.First(p => p.ParameterName == parameterName);

    protected override void SetParameter(int index, DbParameter value) =>
        _parameters[index] = (FakeParameter)value;

    protected override void SetParameter(string parameterName, DbParameter value)
    {
        var index = IndexOf(parameterName);
        if (index >= 0)
            SetParameter(index, value);
        else
            Add(value);
    }
}
