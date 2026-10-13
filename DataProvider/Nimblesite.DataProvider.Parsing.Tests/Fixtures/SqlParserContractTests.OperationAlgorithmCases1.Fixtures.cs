// Implements [CI-TEST-SHARED-CASE-MATRIX].

namespace Nimblesite.DataProvider.Parsing.Tests;

public partial class SqlParserContractTests
{
    /// <summary>Independent named scenario inputs and expectations.</summary>
    public static IEnumerable<object?[]> At_id_is_extracted_ContextCasesData()
    {
        {
            yield return ["At_id_is_extracted", "SELECT * FROM users WHERE id = @id"];
        }
        {
            yield return
            [
                "Repeated_params_are_deduped",
                "SELECT * FROM users WHERE id = @id OR parent_id = @id",
            ];
        }
    }
}
