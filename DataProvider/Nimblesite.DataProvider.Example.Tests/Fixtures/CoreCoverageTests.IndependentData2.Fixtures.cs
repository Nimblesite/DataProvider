// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Example.Tests;

public partial class CoreCoverageTests
{
    private static global::Nimblesite.DataProvider.Core.DatabaseColumn Dataset_DatabaseColumn_CanBeCreated_1() =>
        new Nimblesite.DataProvider.Core.DatabaseColumn
        {
            Name = "Id",
            SqlType = "TEXT",
            CSharpType = "string",
            IsNullable = false,
            IsPrimaryKey = true,
            IsIdentity = false,
            IsComputed = false,
            MaxLength = 50,
            Precision = 10,
            Scale = 2,
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_DatabaseTable_ComputedProperties_Work_1() =>
        new List<Nimblesite.DataProvider.Core.DatabaseColumn>
        {
            new()
            {
                Name = "Id",
                SqlType = "TEXT",
                CSharpType = "string",
                IsPrimaryKey = true,
            },
            new()
            {
                Name = "Name",
                SqlType = "TEXT",
                CSharpType = "string",
            },
            new()
            {
                Name = "AutoId",
                SqlType = "INTEGER",
                CSharpType = "int",
                IsIdentity = true,
            },
            new()
            {
                Name = "Computed",
                SqlType = "TEXT",
                CSharpType = "string",
                IsComputed = true,
            },
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_SqlQueryMetadata_CanBeCreated_1() =>
        new List<Nimblesite.DataProvider.Core.DatabaseColumn>
        {
            new()
            {
                Name = "Id",
                SqlType = "TEXT",
                CSharpType = "string",
            },
        };

    private static global::Nimblesite.DataProvider.Core.GroupingConfig Dataset_GroupingConfig_CanBeCreated_1(
        global::Nimblesite.DataProvider.Core.EntityConfig parent,
        global::Nimblesite.DataProvider.Core.EntityConfig child
    ) =>
        new Nimblesite.DataProvider.Core.GroupingConfig(
            QueryName: "GetInvoices",
            GroupingStrategy: "ParentChild",
            ParentEntity: parent,
            ChildEntity: child
        );

    private static global::Nimblesite.DataProvider.Core.QueryConfigItem Dataset_QueryConfigItem_CanBeCreated_1() =>
        new Nimblesite.DataProvider.Core.QueryConfigItem
        {
            Name = "GetInvoices",
            SqlFile = "GetInvoices.sql",
            GroupingFile = "GetInvoices.grouping.json",
        };

    private static global::Nimblesite.DataProvider.Core.TableConfigItem Dataset_TableConfigItem_CanBeCreated_1() =>
        new Nimblesite.DataProvider.Core.TableConfigItem
        {
            Name = "Invoice",
            Schema = "main",
            GenerateInsert = true,
            GenerateUpdate = true,
            GenerateDelete = false,
            ExcludeColumns = ["computed"],
            PrimaryKeyColumns = ["Id"],
        };

    private static global::Nimblesite.DataProvider.Core.SourceGeneratorDataProviderConfiguration Dataset_SourceGeneratorConfig_CanBeCreated_1() =>
        new Nimblesite.DataProvider.Core.SourceGeneratorDataProviderConfiguration
        {
            ConnectionString = "test",
        };
}
