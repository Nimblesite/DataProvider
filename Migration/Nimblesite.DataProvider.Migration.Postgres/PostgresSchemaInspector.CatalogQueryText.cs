// Implements [MIG-CATALOG-INDEPENDENT-QUERY-DATA].
// Independent catalog query specifications. Execution and row projection stay in the inspector.
namespace Nimblesite.DataProvider.Migration.Postgres;

public static partial class PostgresSchemaInspector
{
    private const string CatalogQuery_InspectTable_1 = """
        SELECT
            c.column_name,
            c.data_type,
            c.is_nullable,
            c.column_default,
            c.character_maximum_length,
            c.numeric_precision,
            c.numeric_scale,
            c.udt_name,
            a.atttypmod
        FROM information_schema.columns c
        LEFT JOIN pg_catalog.pg_namespace n
            ON n.nspname = c.table_schema
        LEFT JOIN pg_catalog.pg_class cls
            ON cls.relname = c.table_name AND cls.relnamespace = n.oid
        LEFT JOIN pg_catalog.pg_attribute a
            ON a.attrelid = cls.oid AND a.attname = c.column_name AND a.attnum > 0
        WHERE c.table_schema = @schema AND c.table_name = @table
        ORDER BY c.ordinal_position
        """;
    private const string CatalogQuery_InspectTable_2 = """
        SELECT kcu.column_name
        FROM information_schema.table_constraints tc
        JOIN information_schema.key_column_usage kcu 
            ON tc.constraint_name = kcu.constraint_name 
            AND tc.table_schema = kcu.table_schema
        WHERE tc.table_schema = @schema 
        AND tc.table_name = @table 
        AND tc.constraint_type = 'PRIMARY KEY'
        ORDER BY kcu.ordinal_position
        """;
    private const string CatalogQuery_InspectTable_3 = """
        SELECT
            i.relname AS index_name,
            ix.indisunique AS is_unique,
            pg_get_indexdef(ix.indexrelid) AS index_def,
            (SELECT array_agg(a.attname ORDER BY ord.n)
             FROM unnest(ix.indkey) WITH ORDINALITY AS ord(attnum, n)
             LEFT JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ord.attnum
             WHERE ord.attnum > 0) AS column_names,
            (SELECT bool_or(ord.attnum = 0)
             FROM unnest(ix.indkey) WITH ORDINALITY AS ord(attnum, n)) AS has_expressions
        FROM pg_class t
        JOIN pg_index ix ON t.oid = ix.indrelid
        JOIN pg_class i ON i.oid = ix.indexrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        LEFT JOIN pg_constraint owned_constraint
            ON owned_constraint.conindid = ix.indexrelid
            AND owned_constraint.contype IN ('p', 'u')
        WHERE n.nspname = @schema
        AND t.relname = @table
        AND NOT ix.indisprimary
        AND owned_constraint.oid IS NULL
        ORDER BY i.relname
        """;
    private const string CatalogQuery_InspectTable_4 = """
        SELECT
            constraint_record.conname,
            array_agg(source_column.attname::text ORDER BY keys.n),
            referenced_table.relname,
            referenced_schema.nspname,
            array_agg(referenced_column.attname::text ORDER BY keys.n),
            CASE constraint_record.confdeltype
                WHEN 'c' THEN 'CASCADE'
                WHEN 'n' THEN 'SET NULL'
                WHEN 'd' THEN 'SET DEFAULT'
                WHEN 'r' THEN 'RESTRICT'
                ELSE 'NO ACTION'
            END,
            CASE constraint_record.confupdtype
                WHEN 'c' THEN 'CASCADE'
                WHEN 'n' THEN 'SET NULL'
                WHEN 'd' THEN 'SET DEFAULT'
                WHEN 'r' THEN 'RESTRICT'
                ELSE 'NO ACTION'
            END
        FROM pg_constraint constraint_record
        JOIN pg_class source_table ON source_table.oid = constraint_record.conrelid
        JOIN pg_namespace source_schema ON source_schema.oid = source_table.relnamespace
        JOIN pg_class referenced_table ON referenced_table.oid = constraint_record.confrelid
        JOIN pg_namespace referenced_schema ON referenced_schema.oid = referenced_table.relnamespace
        JOIN unnest(constraint_record.conkey, constraint_record.confkey)
            WITH ORDINALITY AS keys(source_attnum, referenced_attnum, n) ON true
        JOIN pg_attribute source_column
            ON source_column.attrelid = source_table.oid
            AND source_column.attnum = keys.source_attnum
        JOIN pg_attribute referenced_column
            ON referenced_column.attrelid = referenced_table.oid
            AND referenced_column.attnum = keys.referenced_attnum
        WHERE source_schema.nspname = @schema
        AND source_table.relname = @table
        AND constraint_record.contype = 'f'
        GROUP BY constraint_record.oid, referenced_table.relname, referenced_schema.nspname
        ORDER BY constraint_record.conname
        """;
    private const string CatalogQuery_InspectUniqueConstraints_5 = """
        SELECT
            c.conname,
            array_agg(a.attname ORDER BY keys.n) AS columns
        FROM pg_constraint c
        JOIN pg_class t ON t.oid = c.conrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        JOIN unnest(c.conkey) WITH ORDINALITY AS keys(attnum, n) ON true
        JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = keys.attnum
        WHERE n.nspname = @schema
        AND t.relname = @table
        AND c.contype = 'u'
        GROUP BY c.conname
        ORDER BY c.conname
        """;
    private const string CatalogQuery_InspectCheckConstraints_6 = """
        SELECT
            c.conname,
            pg_get_expr(c.conbin, c.conrelid, true),
            COALESCE(array_length(c.conkey, 1), 0),
            column_ref.attname
        FROM pg_constraint c
        JOIN pg_class t ON t.oid = c.conrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        LEFT JOIN LATERAL (
            SELECT a.attname
            FROM unnest(c.conkey) AS keys(attnum)
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = keys.attnum
        ) AS column_ref ON COALESCE(array_length(c.conkey, 1), 0) = 1
        WHERE n.nspname = @schema
        AND t.relname = @table
        AND c.contype = 'c'
        ORDER BY c.conname
        """;
    private const string CatalogQuery_InspectRls_7 = """
        SELECT c.relrowsecurity, c.relforcerowsecurity
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = @schema AND c.relname = @table
        """;
    private const string CatalogQuery_InspectRls_8 = """
        SELECT policyname, permissive, cmd, roles, qual, with_check
        FROM pg_policies
        WHERE schemaname = @schema AND tablename = @table
        ORDER BY policyname
        """;
}
