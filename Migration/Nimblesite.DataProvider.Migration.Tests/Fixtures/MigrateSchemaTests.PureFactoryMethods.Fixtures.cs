// Implements [CI-TEST-INDEPENDENT-SCHEMA-DATA].
// Pure immutable fixture definitions; assertions and database workflows remain analysed.

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial class MigrateSchemaTests
{
    private static SchemaDefinition CreateTestSchema() =>
        Schema
            .Define("MigrateSchemaTest")
            .Table(
                "public",
                "countries",
                t =>
                    t.Column(
                            "id",
                            PortableTypes.Uuid,
                            c => c.PrimaryKey().Default("gen_random_uuid()")
                        )
                        .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
                        .Column("code", PortableTypes.VarChar(10), c => c.NotNull())
                        .Unique("uq_countries_code", "code")
            )
            .Table(
                "public",
                "regions",
                t =>
                    t.Column(
                            "id",
                            PortableTypes.Uuid,
                            c => c.PrimaryKey().Default("gen_random_uuid()")
                        )
                        .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
                        .Column("country_id", PortableTypes.Uuid, c => c.NotNull())
                        .ForeignKey("country_id", "countries", "id", ForeignKeyAction.Cascade)
                        .Index("idx_regions_country", "country_id")
            )
            .Table(
                "public",
                "suburbs",
                t =>
                    t.Column(
                            "id",
                            PortableTypes.Uuid,
                            c => c.PrimaryKey().Default("gen_random_uuid()")
                        )
                        .Column("name", PortableTypes.VarChar(100), c => c.NotNull())
                        .Column("region_id", PortableTypes.Uuid, c => c.NotNull())
                        .ForeignKey("region_id", "regions", "id", ForeignKeyAction.Cascade)
            )
            .Table(
                "public",
                "venues",
                t =>
                    t.Column(
                            "id",
                            PortableTypes.Uuid,
                            c => c.PrimaryKey().Default("gen_random_uuid()")
                        )
                        .Column("name", PortableTypes.VarChar(200), c => c.NotNull())
                        .Column("suburb_id", PortableTypes.Uuid, c => c.NotNull())
                        .Column("address", PortableTypes.VarChar(300))
                        .ForeignKey("suburb_id", "suburbs", "id", ForeignKeyAction.Cascade)
                        .Unique("uq_venues_name_suburb", "name", "suburb_id")
            )
            .Build();
}
