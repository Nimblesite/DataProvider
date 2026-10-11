// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Tests;

public partial class SchemaTypesTests
{
    private static global::Nimblesite.DataProvider.Core.DatabaseColumn Dataset_DatabaseColumn_CanBeCreatedWithAllProperties_1() =>
        new DatabaseColumn
        {
            Name = "CustomerName",
            SqlType = "NVARCHAR(100)",
            CSharpType = "string",
            IsNullable = true,
            IsPrimaryKey = false,
            IsIdentity = false,
            IsComputed = false,
            MaxLength = 100,
            Precision = null,
            Scale = null,
        };

    private static global::Nimblesite.DataProvider.Core.DatabaseColumn Dataset_DatabaseColumn_CanBeCreatedForDecimalColumn_1() =>
        new DatabaseColumn
        {
            Name = "TotalAmount",
            SqlType = "DECIMAL(10,2)",
            CSharpType = "decimal",
            IsNullable = false,
            IsPrimaryKey = false,
            IsIdentity = false,
            IsComputed = false,
            MaxLength = null,
            Precision = 10,
            Scale = 2,
        };

    private static global::Nimblesite.DataProvider.Core.DatabaseColumn Dataset_DatabaseColumn_CanBeCreatedForIdentityColumn_1() =>
        new DatabaseColumn
        {
            Name = "Id",
            SqlType = "INTEGER",
            CSharpType = "int",
            IsNullable = false,
            IsPrimaryKey = true,
            IsIdentity = true,
            IsComputed = false,
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_DatabaseTable_CanBeCreatedWithColumns_1() =>
        new List<DatabaseColumn>
        {
            new()
            {
                Name = "Id",
                IsPrimaryKey = true,
                IsIdentity = true,
            },
            new() { Name = "Name", CSharpType = "string" },
            new() { Name = "Amount", CSharpType = "decimal" },
        };

    private static global::Nimblesite.DataProvider.Core.DatabaseTable Dataset_DatabaseTable_CanBeCreatedWithColumns_2(
        global::System.Collections.ObjectModel.ReadOnlyCollection<global::Nimblesite.DataProvider.Core.DatabaseColumn> columns
    ) =>
        new DatabaseTable
        {
            Schema = "dbo",
            Name = "Invoice",
            Columns = columns,
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_DatabaseTable_PrimaryKeyColumns_ReturnsCorrectColumns_1() =>
        new List<DatabaseColumn>
        {
            new()
            {
                Name = "Id",
                IsPrimaryKey = true,
                IsIdentity = true,
            },
            new() { Name = "SecondaryId", IsPrimaryKey = true },
            new() { Name = "Name", IsPrimaryKey = false },
            new() { Name = "Amount", IsPrimaryKey = false },
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_DatabaseTable_InsertableColumns_ExcludesIdentityAndComputed_1() =>
        new List<DatabaseColumn>
        {
            new()
            {
                Name = "Id",
                IsIdentity = true,
                IsComputed = false,
            },
            new()
            {
                Name = "ComputedField",
                IsIdentity = false,
                IsComputed = true,
            },
            new()
            {
                Name = "Name",
                IsIdentity = false,
                IsComputed = false,
            },
            new()
            {
                Name = "Amount",
                IsIdentity = false,
                IsComputed = false,
            },
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_DatabaseTable_UpdateableColumns_ExcludesPrimaryKeyIdentityAndComputed_1() =>
        new List<DatabaseColumn>
        {
            new()
            {
                Name = "Id",
                IsPrimaryKey = true,
                IsIdentity = true,
                IsComputed = false,
            },
            new()
            {
                Name = "ComputedField",
                IsPrimaryKey = false,
                IsIdentity = false,
                IsComputed = true,
            },
            new()
            {
                Name = "CategoryId",
                IsPrimaryKey = true,
                IsIdentity = false,
                IsComputed = false,
            },
            new()
            {
                Name = "Name",
                IsPrimaryKey = false,
                IsIdentity = false,
                IsComputed = false,
            },
            new()
            {
                Name = "Amount",
                IsPrimaryKey = false,
                IsIdentity = false,
                IsComputed = false,
            },
        };

    private static global::Nimblesite.DataProvider.Core.DatabaseColumn Dataset_DatabaseColumn_RecordEquality_WorksCorrectly_1() =>
        new DatabaseColumn
        {
            Name = "Id",
            CSharpType = "int",
            IsPrimaryKey = true,
        };

    private static global::Nimblesite.DataProvider.Core.DatabaseColumn Dataset_DatabaseColumn_RecordEquality_WorksCorrectly_2() =>
        new DatabaseColumn
        {
            Name = "Id",
            CSharpType = "int",
            IsPrimaryKey = true,
        };

    private static global::Nimblesite.DataProvider.Core.DatabaseColumn Dataset_DatabaseColumn_RecordEquality_WorksCorrectly_3() =>
        new DatabaseColumn
        {
            Name = "Name",
            CSharpType = "string",
            IsPrimaryKey = false,
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_DatabaseTable_RecordEquality_WorksCorrectly_1() =>
        new List<DatabaseColumn>
        {
            new() { Name = "Id", CSharpType = "int" },
        };

    private static global::Nimblesite.DataProvider.Core.DatabaseTable Dataset_DatabaseTable_RecordEquality_WorksCorrectly_2(
        global::System.Collections.ObjectModel.ReadOnlyCollection<global::Nimblesite.DataProvider.Core.DatabaseColumn> columns
    ) =>
        new DatabaseTable
        {
            Schema = "dbo",
            Name = "Invoice",
            Columns = columns,
        };

    private static global::Nimblesite.DataProvider.Core.DatabaseTable Dataset_DatabaseTable_RecordEquality_WorksCorrectly_3(
        global::System.Collections.ObjectModel.ReadOnlyCollection<global::Nimblesite.DataProvider.Core.DatabaseColumn> columns
    ) =>
        new DatabaseTable
        {
            Schema = "dbo",
            Name = "Invoice",
            Columns = columns,
        };

    private static global::Nimblesite.DataProvider.Core.DatabaseTable Dataset_DatabaseTable_RecordEquality_WorksCorrectly_4(
        global::System.Collections.ObjectModel.ReadOnlyCollection<global::Nimblesite.DataProvider.Core.DatabaseColumn> columns
    ) =>
        new DatabaseTable
        {
            Schema = "dbo",
            Name = "InvoiceLine",
            Columns = columns,
        };

    private static global::System.Collections.Generic.List<global::Nimblesite.DataProvider.Core.DatabaseColumn> Dataset_DatabaseTable_ComputedProperties_AreReadOnly_1() =>
        new List<DatabaseColumn>
        {
            new() { Name = "Id", IsPrimaryKey = true },
            new() { Name = "Name", IsPrimaryKey = false },
        };
}
