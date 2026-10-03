namespace Nimblesite.Reporting.React.Components
{
    /// <summary>
    /// Shared column-name lookup used by report components.
    /// </summary>
    internal static class ColumnLookup
    {
        /// <summary>
        /// Returns the index of the named column, or -1 if not found.
        /// </summary>
        internal static int FindColumnIndex(string[] columnNames, string field)
        {
            if (columnNames == null || field == null)
                return -1;
            for (var i = 0; i < columnNames.Length; i++)
            {
                if (columnNames[i] == field)
                    return i;
            }
            return -1;
        }
    }
}
