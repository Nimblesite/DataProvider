// Implements [CI-DESLOP-INDEPENDENT-DATA].
using System.Collections.Immutable;
using SchemaIntegrityOk = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaIntegrityVerifierIssue59Tests
{
    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_TextDefaultStoredWithTypeCast_TreatedAsEqualToBareLiteral_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::System.Boolean isNullable,
        global::System.String? defaultValue
    ) =>
        SchemaTestData.Schema(
            name: "live",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "t",
                    columns:
                    [
                        SchemaTestData.Column(
                            name: "status",
                            type: PortableTypes.Text,
                            isNullable: false,
                            defaultValue: "'pending'::text"
                        ),
                    ]
                ),
            ]
        );

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_TextDefaultStoredWithTypeCast_TreatedAsEqualToBareLiteral_2(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::System.Boolean isNullable,
        global::System.String? defaultValue
    ) =>
        SchemaTestData.Schema(
            name: "desired",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "t",
                    columns:
                    [
                        SchemaTestData.Column(
                            name: "status",
                            type: PortableTypes.Text,
                            isNullable: false,
                            defaultValue: "'pending'"
                        ),
                    ]
                ),
            ]
        );

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_TextDefaultStoredWithTypeCast_TreatedAsEqualToBareLiteral_3(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.ColumnDefinition>? columns,
        global::System.String name,
        global::Nimblesite.DataProvider.Migration.Core.PortableType type,
        global::System.Boolean isNullable,
        global::System.String? defaultValue
    ) =>
        SchemaTestData.Schema(
            name: "changed-default",
            tables:
            [
                SchemaTestData.Table(
                    schema: "public",
                    name: "t",
                    columns:
                    [
                        SchemaTestData.Column(
                            name: "status",
                            type: PortableTypes.Text,
                            isNullable: false,
                            defaultValue: "'failed'"
                        ),
                    ]
                ),
            ]
        );

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_MultiRoleGrantInDesired_MatchedAgainstSingleRoleGrantsFromInspector_1(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresGrantDefinition>? grants
    ) =>
        SchemaTestData.Schema(
            name: "live",
            grants:
            [
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.Schema,
                    privileges: ["USAGE"],
                    roles: ["app_user"]
                ),
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.Schema,
                    privileges: ["USAGE"],
                    roles: ["app_admin"]
                ),
            ]
        );

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_MultiRoleGrantInDesired_MatchedAgainstSingleRoleGrantsFromInspector_2(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresGrantDefinition>? grants,
        global::System.String schema,
        global::Nimblesite.DataProvider.Migration.Core.PostgresGrantTarget target,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? privileges,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? roles
    ) =>
        SchemaTestData.Schema(
            name: "desired",
            grants:
            [
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.Schema,
                    privileges: ["USAGE"],
                    roles: ["app_user", "app_admin"]
                ),
            ]
        );

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_AllTablesInSchemaTarget_MatchedAgainstPerTableGrantsFromInspector_1(
        global::System.String schema,
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.PostgresGrantDefinition>? grants,
        global::System.String schema,
        global::Nimblesite.DataProvider.Migration.Core.PostgresGrantTarget target,
        global::System.String? objectName,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? privileges,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? roles
    ) =>
        SchemaTestData.Schema(
            name: "live",
            tables:
            [
                SchemaTestData.Table(schema: "public", name: "a"),
                SchemaTestData.Table(schema: "public", name: "b"),
            ],
            grants:
            [
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.Table,
                    objectName: "a",
                    privileges: ["SELECT", "INSERT"],
                    roles: ["app_user"]
                ),
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.Table,
                    objectName: "b",
                    privileges: ["SELECT", "INSERT"],
                    roles: ["app_user"]
                ),
            ]
        );

    private global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Fixture_Verify_AllTablesInSchemaTarget_MatchedAgainstPerTableGrantsFromInspector_2(
        global::System.String name,
        global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.DataProvider.Migration.Core.TableDefinition>? tables,
        global::System.String schema,
        global::Nimblesite.DataProvider.Migration.Core.PostgresGrantTarget target,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? privileges,
        global::System.Collections.Generic.IReadOnlyList<global::System.String>? roles
    ) =>
        SchemaTestData.Schema(
            name: "desired",
            tables:
            [
                SchemaTestData.Table(schema: "public", name: "a"),
                SchemaTestData.Table(schema: "public", name: "b"),
            ],
            grants:
            [
                SchemaTestData.PostgresGrant(
                    schema: "public",
                    target: PostgresGrantTarget.AllTablesInSchema,
                    privileges: ["SELECT", "INSERT"],
                    roles: ["app_user"]
                ),
            ]
        );
}
