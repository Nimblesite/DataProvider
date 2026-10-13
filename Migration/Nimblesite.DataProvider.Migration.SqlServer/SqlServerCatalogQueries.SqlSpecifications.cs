// Implements [CI-INDEPENDENT-SQL-SPECIFICATIONS].
// Immutable SQL text only. Execution, binding and projection remain in the analysed source.
namespace Nimblesite.DataProvider.Migration.SqlServer;

internal static partial class SqlServerCatalogQueries
{
    internal const string SqlSpecification1 = """
        SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME
        """;
    internal const string SqlSpecification2 = """
        SELECT c.TABLE_SCHEMA, c.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE,
               c.CHARACTER_MAXIMUM_LENGTH,
               COALESCE(CAST(c.NUMERIC_PRECISION AS INT), CAST(c.DATETIME_PRECISION AS INT)),
               c.NUMERIC_SCALE, c.IS_NULLABLE, c.COLUMN_DEFAULT,
               COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)),
                              c.COLUMN_NAME, 'IsIdentity')
        FROM INFORMATION_SCHEMA.COLUMNS c
        JOIN INFORMATION_SCHEMA.TABLES t
          ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
        WHERE t.TABLE_TYPE = 'BASE TABLE'
        ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION
        """;
    internal const string SqlSpecification3 = """
        SELECT tc.TABLE_SCHEMA, tc.TABLE_NAME, tc.CONSTRAINT_NAME, tc.CONSTRAINT_TYPE, k.COLUMN_NAME
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE k
          ON k.CONSTRAINT_SCHEMA = tc.CONSTRAINT_SCHEMA AND k.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
        WHERE tc.CONSTRAINT_TYPE IN ('PRIMARY KEY', 'UNIQUE')
        ORDER BY tc.TABLE_SCHEMA, tc.TABLE_NAME, tc.CONSTRAINT_NAME, k.ORDINAL_POSITION
        """;
    internal const string SqlSpecification4 = """
        SELECT SCHEMA_NAME(fk.schema_id), OBJECT_NAME(fk.parent_object_id), fk.name,
               COL_NAME(fkc.parent_object_id, fkc.parent_column_id),
               SCHEMA_NAME(rt.schema_id), rt.name,
               COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id),
               fk.delete_referential_action_desc, fk.update_referential_action_desc
        FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
        JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
        ORDER BY 1, 2, 3, fkc.constraint_column_id
        """;
    internal const string SqlSpecification5 = """
        SELECT SCHEMA_NAME(t.schema_id), t.name, i.name, i.is_unique, i.filter_definition,
               COL_NAME(ic.object_id, ic.column_id)
        FROM sys.indexes i
        JOIN sys.tables t ON t.object_id = i.object_id
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        WHERE i.is_primary_key = 0 AND i.is_unique_constraint = 0 AND i.type > 0
          AND ic.is_included_column = 0
        ORDER BY 1, 2, 3, ic.key_ordinal
        """;
    internal const string SqlSpecification6 = """
        SELECT SCHEMA_NAME(t.schema_id), t.name, cc.name,
               COL_NAME(cc.parent_object_id, NULLIF(cc.parent_column_id, 0)), cc.definition
        FROM sys.check_constraints cc
        JOIN sys.tables t ON t.object_id = cc.parent_object_id
        ORDER BY 1, 2, 3
        """;
}
