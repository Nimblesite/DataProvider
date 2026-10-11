// Implements [DP-CLI-SHARED-EMISSION-TEXT].
// Immutable generated-source text only; rendering remains analysed.
namespace Nimblesite.DataProvider.Core.CodeGeneration;

public static partial class DataAccessGenerator
{
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines1 =
    [
        "",
        "                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))",
        "                {",
        "                    while (await reader.ReadAsync().ConfigureAwait(false))",
        "                    {",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines2 =
    [
        "                        );",
        "                        results.Add(item);",
        "                    }",
        "                }",
        "            }",
        "",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines3 =
    [
        "        }",
        "        catch (Exception ex)",
        "        {",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines4 =
    [
        "        }",
        "    }",
        "}",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines5 =
    [
        "",
        "                var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);",
        "                return new Result<int, SqlError>.Ok<int, SqlError>(rowsAffected);",
        "            }",
        "        }",
        "        catch (Exception ex)",
        "        {",
        "            return new Result<int, SqlError>.Error<int, SqlError>(new SqlError(\"Database error\", ex));",
        "        }",
        "    }",
        "}",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines6 =
    [
        "",
        "        if (transaction.Connection is null)",
        "            return new Result<int, SqlError>.Error<int, SqlError>(new SqlError(\"Transaction has no connection\"));",
        "",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines7 =
    [
        "        try",
        "        {",
        "            using (var command = transaction.Connection.CreateCommand())",
        "            {",
        "                command.Transaction = transaction;",
        "                command.CommandText = sql;",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines8 =
    [
        "            }",
        "        }",
        "        catch (Exception ex)",
        "        {",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines9 =
    [
        "        static void AddParameter(IDbCommand command, string name, object value)",
        "        {",
        "            var parameter = command.CreateParameter();",
        "            parameter.ParameterName = name;",
        "            parameter.Value = value;",
        "            command.Parameters.Add(parameter);",
        "        }",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines10 =
    [
        "        {",
        "            for (int i = 0; i < parameters.Count; i++)",
        "            {",
        "                command.Parameters.AddWithValue(\"@p\" + i, parameters[i] ?? (object)DBNull.Value);",
        "            }",
        "",
        "            var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);",
        "            return new Result<int, SqlError>.Ok<int, SqlError>(rowsAffected);",
        "        }",
    ];
    private static readonly global::System.Collections.Immutable.ImmutableArray<string> EmissionBlockLines12 =
    [
        "",
        "        try",
        "        {",
    ];
}
