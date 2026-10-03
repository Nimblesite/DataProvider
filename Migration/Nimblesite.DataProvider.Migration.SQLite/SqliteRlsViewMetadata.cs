using SqlParser;
using SqlParser.Dialects;
using SqlParser.Tokens;

namespace Nimblesite.DataProvider.Migration.SQLite;

// Implements [RLS-DIFF]: a SELECT-only policy has no trigger carrying its name.
internal static class SqliteRlsViewMetadata
{
    private const string Prefix = "RLS-SQLITE-POLICY:";

    internal static string Encode(string policyName) =>
        $"/*{Prefix}{Convert.ToHexString(Encoding.UTF8.GetBytes(policyName))}*/";

    internal static string? ReadPolicyName(string sql)
    {
        try
        {
            var metadata = new Tokenizer(unescape: false)
                .Tokenize(sql: sql, dialect: new SQLiteDialect())
                .OfType<Whitespace>()
                .Where(token => token.WhitespaceKind == WhitespaceKind.MultilineComment)
                .Select(token => token.Value)
                .FirstOrDefault(value =>
                    value?.StartsWith(Prefix, StringComparison.Ordinal) == true
                );
            return metadata is null
                ? null
                : Encoding.UTF8.GetString(Convert.FromHexString(metadata[Prefix.Length..]));
        }
        catch (Exception error) when (error is TokenizeException or FormatException)
        {
            return null;
        }
    }
}
