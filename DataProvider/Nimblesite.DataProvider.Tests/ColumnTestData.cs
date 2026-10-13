namespace Nimblesite.DataProvider.Tests;

// Implements [DP-TEST-COLUMN-FIXTURE].
internal static class ColumnTestData
{
    internal static DatabaseColumn Create(
        string name = "",
        string sqlType = "",
        string cSharpType = "",
        bool isNullable = false,
        bool isPrimaryKey = false,
        bool isIdentity = false,
        bool isComputed = false,
        int? maxLength = null,
        int? precision = null,
        int? scale = null
    ) =>
        new()
        {
            Name = name,
            SqlType = sqlType,
            CSharpType = cSharpType,
            IsNullable = isNullable,
            IsPrimaryKey = isPrimaryKey,
            IsIdentity = isIdentity,
            IsComputed = isComputed,
            MaxLength = maxLength,
            Precision = precision,
            Scale = scale,
        };
}
