// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Tests;

public partial class ConfigurationTypesTests
{
    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.TableConfig> Dataset_DataProviderConfig_CanBeCreatedWithTablesAndConnectionString_1() =>
        new List<TableConfig>
        {
            new() { Name = "Invoice", Schema = "dbo" },
        };

    private static global::Nimblesite.DataProvider.Core.DataProviderConfig Dataset_DataProviderConfig_CanBeCreatedWithTablesAndConnectionString_2(
        global::System.Collections.ObjectModel.ReadOnlyCollection<global::Nimblesite.DataProvider.Core.TableConfig> tables
    ) => new DataProviderConfig { Tables = tables, ConnectionString = "Data Source=test.db" };

    private static global::Nimblesite.DataProvider.Core.EntityConfig Dataset_GroupingConfig_CanBeCreated_1() =>
        new EntityConfig("InvoiceLine", ["LineId"], ["LineId", "Description"], ["InvoiceId"]);

    private static global::Nimblesite.DataProvider.Core.EntityConfig Dataset_EntityConfig_CanBeCreatedWithParentKeyColumns_1() =>
        new EntityConfig("InvoiceLine", ["LineId"], ["LineId", "Description"], ["InvoiceId"]);

    private static global::Nimblesite.DataProvider.Core.GroupingConfig Dataset_GroupingConfig_JsonSerialization_WorksCorrectly_1() =>
        new GroupingConfig(
            "GetInvoices",
            "ParentChild",
            new EntityConfig("Invoice", ["Id"], ["Id", "InvoiceNumber", "CustomerName"]),
            new EntityConfig(
                "InvoiceLine",
                ["LineId"],
                ["LineId", "Description", "Amount"],
                ["InvoiceId"]
            )
        );

    private static global::Nimblesite.DataProvider.Core.EntityConfig Dataset_GroupingConfig_RecordEquality_WorksCorrectly_1() =>
        new EntityConfig("InvoiceLine", ["LineId"], ["LineId", "Description"], ["InvoiceId"]);

    private static global::Nimblesite.DataProvider.Core.GroupingConfig Dataset_GroupingConfig_RecordEquality_WorksCorrectly_2(
        global::Nimblesite.DataProvider.Core.EntityConfig parentEntity,
        global::Nimblesite.DataProvider.Core.EntityConfig childEntity
    ) => new GroupingConfig("DifferentQuery", "ParentChild", parentEntity, childEntity);

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.TableConfig> Dataset_DataProviderConfig_RecordEquality_WorksCorrectly_1() =>
        new List<TableConfig>
        {
            new() { Name = "Invoice", Schema = "dbo" },
        };
}
