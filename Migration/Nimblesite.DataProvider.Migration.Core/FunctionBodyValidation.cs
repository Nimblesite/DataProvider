namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Shared validation for PostgreSQL function support objects. The raw
/// <c>body</c> and the LQL <c>bodyLql</c> are mutually exclusive: the LQL body
/// is transpiled into the raw body when DDL is generated, so a definition that
/// declares both is ambiguous. Implements [MIG-FUNCTION-BODY-EXCLUSIVE].
/// </summary>
internal static class FunctionBodyValidation
{
    /// <summary>
    /// Validates that a function declares at most one body source.
    /// </summary>
    internal static void EnsureMutuallyExclusive(PostgresFunctionDefinition function)
    {
        if (
            !string.IsNullOrWhiteSpace(function.Body)
            && !string.IsNullOrWhiteSpace(function.BodyLql)
        )
        {
            throw new InvalidOperationException(
                "PostgreSQL function body and bodyLql are mutually exclusive: "
                    + $"{function.Schema}.{function.Name}"
            );
        }
    }
}
