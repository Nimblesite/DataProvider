using System.Data.Common;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

// Implements [CON-SHARED-CORE]. Generic ADO.NET implementation of
// IDatabaseEffects that works for any driver whose connection derives from
// DbConnection. Platforms inject a connection factory + a type mapper and
// everything else (open, bind dummy parameters, read reader schema) lives
// here. SQLite and Postgres both consume this so neither duplicates the
// reader-loop boilerplate.
/// <summary>
/// A driver-agnostic <see cref="IDatabaseEffects"/> that opens a connection
/// through the supplied factory, binds dummy parameter values, executes the
/// SQL, and reads back column metadata from the resulting
/// <see cref="DbDataReader"/>. Type mapping is delegated to a supplied
/// <see cref="TypeMapper"/> so the class itself has zero dialect knowledge.
/// </summary>
public sealed class AdoNetDatabaseEffects : IDatabaseEffects
{
    /// <summary>
    /// Maps a CLR <see cref="Type"/> + the driver's declared SQL data type
    /// name to a C# type literal (e.g. <c>"int?"</c>, <c>"string"</c>).
    /// </summary>
    /// <param name="fieldType">The CLR type reported by the reader.</param>
    /// <param name="dataTypeName">The driver-declared SQL type name.</param>
    /// <param name="isNullable">Whether the column is nullable.</param>
    /// <returns>A C# type literal suitable for emission in generated code.</returns>
    public delegate string TypeMapper(Type fieldType, string dataTypeName, bool isNullable);

    private readonly Func<string, DbConnection> _connectionFactory;
    private readonly TypeMapper _typeMapper;

    /// <summary>
    /// Creates a new <see cref="AdoNetDatabaseEffects"/>.
    /// </summary>
    /// <param name="connectionFactory">
    /// Builds a driver-specific <see cref="DbConnection"/> from a connection
    /// string (e.g. <c>cs =&gt; new SqliteConnection(cs)</c>).
    /// </param>
    /// <param name="typeMapper">Resolves a C# type literal from a reader column.</param>
    public AdoNetDatabaseEffects(
        Func<string, DbConnection> connectionFactory,
        TypeMapper typeMapper
    )
    {
        _connectionFactory = connectionFactory;
        _typeMapper = typeMapper;
    }

    /// <inheritdoc />
    public async Task<
        Result<IReadOnlyList<DatabaseColumn>, SqlError>
    > GetColumnMetadataFromSqlAsync(
        string connectionString,
        string sql,
        IEnumerable<ParameterInfo> parameters
    )
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return Error(new SqlError("connectionString cannot be null or empty"));

        if (string.IsNullOrWhiteSpace(sql))
            return Error(new SqlError("sql cannot be null or empty"));

        if (parameters == null)
            return Error(new SqlError("parameters cannot be null"));

        try
        {
            using var connection = _connectionFactory(connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

#pragma warning disable CA2100 // SQL is author-supplied at codegen time, not user input
            using var command = connection.CreateCommand();
            command.CommandText = sql;
#pragma warning restore CA2100

            BindDummyParameters(command, parameters);

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            var columns = ReadColumns(reader);
            return Ok(columns);
        }
        catch (DbException ex)
        {
            return Error(new SqlError($"Failed to get column metadata: {ex.Message}", ex));
        }
        catch (InvalidOperationException ex)
        {
            return Error(new SqlError("Unexpected error getting column metadata", ex));
        }
    }

    private static void BindDummyParameters(
        DbCommand command,
        IEnumerable<ParameterInfo> parameters
    )
    {
        foreach (var param in parameters)
        {
            var dbParam = command.CreateParameter();
            dbParam.ParameterName = $"@{param.Name}";
            dbParam.Value = DummyParameterValues.GetDummyValueForParameter(param);
            command.Parameters.Add(dbParam);
        }
    }

    private IReadOnlyList<DatabaseColumn> ReadColumns(DbDataReader reader)
    {
        var columns = new List<DatabaseColumn>(reader.FieldCount);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            var fieldType = reader.GetFieldType(i);
            var dataTypeName = reader.GetDataTypeName(i);
            var isNullable =
                !fieldType.IsValueType || Nullable.GetUnderlyingType(fieldType) != null;
            var csharpType = _typeMapper(fieldType, dataTypeName, isNullable);

            columns.Add(
                new DatabaseColumn
                {
                    Name = columnName,
                    SqlType = dataTypeName,
                    CSharpType = csharpType,
                    IsNullable = isNullable,
                    IsPrimaryKey = false,
                    IsIdentity = false,
                    IsComputed = false,
                }
            );
        }
        return columns.AsReadOnly();
    }

    private static Result<IReadOnlyList<DatabaseColumn>, SqlError> Ok(
        IReadOnlyList<DatabaseColumn> value
    ) =>
        new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
            IReadOnlyList<DatabaseColumn>,
            SqlError
        >(value);

    private static Result<IReadOnlyList<DatabaseColumn>, SqlError> Error(SqlError error) =>
        new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error<
            IReadOnlyList<DatabaseColumn>,
            SqlError
        >(error);
}
