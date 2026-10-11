using System.Globalization;
using System.Text;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

/// <summary>
/// Shared batching template for generated bulk operations.
/// </summary>
// Implements [DP-CODEGEN-SHARED-BULK-LOOP].
public static partial class DataAccessGenerator
{
    private const string BulkBatchLoopTemplate = """
            {{
                const int batchSize = {1};
                var {2} = 0;
                var batch = new List<({0})>(batchSize);

                try
                {{
                    foreach (var record in records)
                    {{
                        batch.Add(record);
                        if (batch.Count >= batchSize)
                        {{
                            var result = await ExecuteBulk{3}{4}BatchAsync(transaction, batch).ConfigureAwait(false);
                            if (result is Result<int, SqlError>.Error<int, SqlError> err)
                                return err;
                            {2} += ((Result<int, SqlError>.Ok<int, SqlError>)result).Value;
                            batch.Clear();
                        }}
                    }}

                    if (batch.Count > 0)
                    {{
                        var finalResult = await ExecuteBulk{3}{4}BatchAsync(transaction, batch).ConfigureAwait(false);
                        if (finalResult is Result<int, SqlError>.Error<int, SqlError> finalErr)
                            return finalErr;
                        {2} += ((Result<int, SqlError>.Ok<int, SqlError>)finalResult).Value;
                    }}

                    return new Result<int, SqlError>.Ok<int, SqlError>({2});
                }}
                catch (Exception ex)
                {{
                    return new Result<int, SqlError>.Error<int, SqlError>(new SqlError("{5}", ex));
                }}
            }}

        """;

    private static readonly CompositeFormat BulkBatchLoopFormat = CompositeFormat.Parse(
        format: BulkBatchLoopTemplate
    );

    private static void AppendBulkBatchLoop(
        StringBuilder builder,
        string tableName,
        string tupleType,
        int batchSize,
        string operation,
        string totalVariable,
        string failure
    )
    {
        var loop = string.Format(
            provider: CultureInfo.InvariantCulture,
            format: BulkBatchLoopFormat,
            args: [tupleType, batchSize, totalVariable, operation, tableName, failure]
        );
        foreach (var line in loop.ReplaceLineEndings(replacementText: "\n").Split(separator: '\n'))
        {
            builder.AppendLine(value: line);
        }
    }
}
