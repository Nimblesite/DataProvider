// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class PostgresDropConstraintBackedIndexTests
{
    private static SchemaDefinition SchemaWithUniqueConstraint() =>
        Schema
            .Define("Issue49")
            .Table(
                SchemaName,
                TableName,
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("key_hash", PortableTypes.VarChar(255), c => c.NotNull())
                        .Unique(ConstraintName, "key_hash")
            )
            .Build();

    private static SchemaDefinition SchemaWithoutUniqueConstraint() =>
        Schema
            .Define("Issue49")
            .Table(
                SchemaName,
                TableName,
                t =>
                    t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
                        .Column("key_hash", PortableTypes.VarChar(255), c => c.NotNull())
            )
            .Build();
}
