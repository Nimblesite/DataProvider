// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class PostgresMigrationTests
{
    private const string Data_CreateTable_PostgresNativeTypes_Success_1 = """
        SELECT column_name, data_type
        FROM information_schema.columns
        WHERE table_name = 'type_test'
        """;

    private const string Data_ExpressionIndex_CreateWithLowerFunction_Success_1 = """
        SELECT indexname, indexdef
        FROM pg_indexes
        WHERE tablename = 'artists' AND indexname = 'uq_artists_name'
        """;

    private const string Data_ExpressionIndex_MultiExpression_CompositeIndexSuccess_1 = """
        SELECT indexdef
        FROM pg_indexes
        WHERE tablename = 'places' AND indexname = 'uq_places_name_suburb'
        """;

    private const string Data_ExpressionIndex_MultiExpression_AllowsSameNameDifferentSuburb_1 = """
        INSERT INTO regions (id, name) VALUES
        ('11111111-1111-1111-1111-111111111111', 'Melbourne'),
        ('22222222-2222-2222-2222-222222222222', 'Sydney')
        """;

    private const string Data_ExpressionIndex_MultiExpression_AllowsSameNameDifferentSuburb_2 = """
        INSERT INTO locations (name, region_id) VALUES
        ('The Corner', '11111111-1111-1111-1111-111111111111'),
        ('The Corner', '22222222-2222-2222-2222-222222222222')
        """;

    private const string Data_LqlDefault_GenUuidFunction_GeneratesValidUuid_1 =
        @"
            INSERT INTO items (name) VALUES ('Item 1');
            INSERT INTO items (name) VALUES ('Item 2');
            INSERT INTO items (name) VALUES ('Item 3');
        ";
}
