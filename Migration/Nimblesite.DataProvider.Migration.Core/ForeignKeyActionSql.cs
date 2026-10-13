namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Renders a <see cref="ForeignKeyAction" /> as the SQL referential action
/// keyword shared by every relational provider. Implements
/// [MIG-FK-ACTION-SQL].
/// </summary>
internal static class ForeignKeyActionSql
{
    /// <summary>
    /// Maps a foreign-key action to its SQL keyword (for example
    /// <c>ON DELETE CASCADE</c>).
    /// </summary>
    internal static string ToSql(ForeignKeyAction action) =>
        action switch
        {
            ForeignKeyAction.NoAction => "NO ACTION",
            ForeignKeyAction.Cascade => "CASCADE",
            ForeignKeyAction.SetNull => "SET NULL",
            ForeignKeyAction.SetDefault => "SET DEFAULT",
            ForeignKeyAction.Restrict => "RESTRICT",
            _ => "NO ACTION",
        };
}
