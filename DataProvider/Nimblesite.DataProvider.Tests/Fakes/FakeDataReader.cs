using System.Collections;

namespace Nimblesite.DataProvider.Tests.Fakes;

/// <summary>
/// Fake data reader for testing that returns predefined data
/// </summary>
internal sealed class FakeDataReader : DbDataReader
{
    private readonly object[][] _data;
    private readonly string[] _columnNames;
    private readonly Type[] _columnTypes;
    private int _currentRow = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeDataReader"/> class
    /// </summary>
    /// <param name="columnNames">The column names</param>
    /// <param name="columnTypes">The column types</param>
    /// <param name="data">The data rows</param>
    public FakeDataReader(string[] columnNames, Type[] columnTypes, object[][] data)
    {
        _columnNames = columnNames ?? throw new ArgumentNullException(nameof(columnNames));
        _columnTypes = columnTypes ?? throw new ArgumentNullException(nameof(columnTypes));
        _data = data ?? throw new ArgumentNullException(nameof(data));

        if (columnNames.Length != columnTypes.Length)
            throw new ArgumentException("Column names and types must have the same length");
    }

    public override int Depth => 0;

    public override int FieldCount => _columnNames.Length;

    public override bool HasRows => _data.Length > 0;

    public override bool IsClosed => false;

    public override int RecordsAffected => _data.Length;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool GetBoolean(int ordinal) =>
        Convert.ToBoolean(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    public override byte GetByte(int ordinal) =>
        Convert.ToByte(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    public override long GetBytes(
        int ordinal,
        long dataOffset,
        byte[]? buffer,
        int bufferOffset,
        int length
    )
    {
        var value = (byte[])GetValue(ordinal);
        if (buffer == null)
            return value.Length;

        var copyLength = Math.Min(length, value.Length - (int)dataOffset);
        Array.Copy(value, dataOffset, buffer, bufferOffset, copyLength);
        return copyLength;
    }

    public override char GetChar(int ordinal) =>
        Convert.ToChar(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    public override long GetChars(
        int ordinal,
        long dataOffset,
        char[]? buffer,
        int bufferOffset,
        int length
    )
    {
        var value = GetString(ordinal);
        if (buffer == null)
            return value.Length;

        var copyLength = Math.Min(length, value.Length - (int)dataOffset);
        value.CopyTo((int)dataOffset, buffer, bufferOffset, copyLength);
        return copyLength;
    }

    public override string GetDataTypeName(int ordinal) => _columnTypes[ordinal].Name;

    public override DateTime GetDateTime(int ordinal) =>
        Convert.ToDateTime(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    public override decimal GetDecimal(int ordinal) =>
        Convert.ToDecimal(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    public override double GetDouble(int ordinal) =>
        Convert.ToDouble(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    public double GetDouble(string name) => GetDouble(GetOrdinal(name));

    public override Type GetFieldType(int ordinal) => _columnTypes[ordinal];

    public override float GetFloat(int ordinal) =>
        Convert.ToSingle(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);

    public override short GetInt16(int ordinal) =>
        Convert.ToInt16(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    public override int GetInt32(int ordinal) =>
        Convert.ToInt32(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    public int GetInt32(string name) => GetInt32(GetOrdinal(name));

    public override long GetInt64(int ordinal) =>
        Convert.ToInt64(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    public override string GetName(int ordinal) => _columnNames[ordinal];

    public override int GetOrdinal(string name) => Array.IndexOf(_columnNames, name);

    public override string GetString(int ordinal) =>
        Convert.ToString(GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture)
        ?? string.Empty;

    public string GetString(string name) => GetString(GetOrdinal(name));

    public override object GetValue(int ordinal)
    {
        if (_currentRow < 0 || _currentRow >= _data.Length)
            throw new InvalidOperationException("No current row");

        return _data[_currentRow][ordinal];
    }

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (int i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }
        return count;
    }

    public override bool IsDBNull(int ordinal) => GetValue(ordinal) == DBNull.Value;

    public bool IsDBNull(string name) => IsDBNull(GetOrdinal(name));

    public override bool NextResult() => false;

    public override bool Read()
    {
        _currentRow++;
        return _currentRow < _data.Length;
    }

    public override Task<bool> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Read());

    public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) =>
        Task.FromResult(IsDBNull(ordinal));

    public override IEnumerator GetEnumerator() => new DbEnumerator(this);

    protected override void Dispose(bool disposing) => base.Dispose(disposing);
}
