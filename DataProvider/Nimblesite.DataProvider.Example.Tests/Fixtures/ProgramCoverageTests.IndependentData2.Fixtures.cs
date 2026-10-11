// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Example.Tests;

public partial class ProgramCoverageTests
{
    private static global::Nimblesite.DataProvider.Example.Model.Order Dataset_ModelTypes_CanBeConstructed_1() =>
        new Nimblesite.DataProvider.Example.Model.Order(
            Id: 1,
            OrderNumber: "ORD-001",
            OrderDate: "2024-01-01",
            CustomerId: 1,
            TotalAmount: 100.0,
            Status: "Completed",
            Items:
            [
                new Nimblesite.DataProvider.Example.Model.OrderItem(
                    Id: 1,
                    OrderId: 1,
                    ProductName: "Widget",
                    Quantity: 2.0,
                    Price: 50.0,
                    Subtotal: 100.0
                ),
            ]
        );

    private static global::Nimblesite.DataProvider.Example.Model.Customer Dataset_ModelTypes_CanBeConstructed_2() =>
        new Nimblesite.DataProvider.Example.Model.Customer(
            Id: 1,
            CustomerName: "Test Corp",
            Email: "test@test.com",
            Phone: "555-1234",
            CreatedDate: "2024-01-01",
            Addresses:
            [
                new Nimblesite.DataProvider.Example.Model.Address(
                    Id: 1,
                    CustomerId: 1,
                    Street: "123 Main St",
                    City: "TestCity",
                    State: "TS",
                    ZipCode: "12345",
                    Country: "USA"
                ),
            ]
        );

    private static global::Nimblesite.DataProvider.Example.Model.Order Dataset_ModelRecords_EqualityWorks_1() =>
        new Nimblesite.DataProvider.Example.Model.Order(
            Id: 1,
            OrderNumber: "ORD-001",
            OrderDate: "2024-01-01",
            CustomerId: 1,
            TotalAmount: 100.0,
            Status: "Completed",
            Items: []
        );

    private static global::Nimblesite.DataProvider.Example.Model.Order Dataset_ModelRecords_EqualityWorks_2() =>
        new Nimblesite.DataProvider.Example.Model.Order(
            Id: 1,
            OrderNumber: "ORD-001",
            OrderDate: "2024-01-01",
            CustomerId: 1,
            TotalAmount: 100.0,
            Status: "Completed",
            Items: []
        );

    private static global::Nimblesite.DataProvider.Example.Model.OrderItem Dataset_ModelRecords_EqualityWorks_3() =>
        new Nimblesite.DataProvider.Example.Model.OrderItem(
            Id: 1,
            OrderId: 1,
            ProductName: "W",
            Quantity: 1.0,
            Price: 10.0,
            Subtotal: 10.0
        );

    private static global::Nimblesite.DataProvider.Example.Model.OrderItem Dataset_ModelRecords_EqualityWorks_4() =>
        new Nimblesite.DataProvider.Example.Model.OrderItem(
            Id: 1,
            OrderId: 1,
            ProductName: "W",
            Quantity: 1.0,
            Price: 10.0,
            Subtotal: 10.0
        );

    private static global::Nimblesite.DataProvider.Example.Model.Address Dataset_ModelRecords_EqualityWorks_5() =>
        new Nimblesite.DataProvider.Example.Model.Address(
            Id: 1,
            CustomerId: 1,
            Street: "St",
            City: "C",
            State: "S",
            ZipCode: "Z",
            Country: "US"
        );

    private static global::Nimblesite.DataProvider.Example.Model.Address Dataset_ModelRecords_EqualityWorks_6() =>
        new Nimblesite.DataProvider.Example.Model.Address(
            Id: 1,
            CustomerId: 1,
            Street: "St",
            City: "C",
            State: "S",
            ZipCode: "Z",
            Country: "US"
        );

    private static global::Nimblesite.DataProvider.Example.Model.Customer Dataset_ModelRecords_EqualityWorks_7() =>
        new Nimblesite.DataProvider.Example.Model.Customer(
            Id: 1,
            CustomerName: "Test",
            Email: null,
            Phone: null,
            CreatedDate: "2024-01-01",
            Addresses: []
        );

    private static global::Nimblesite.DataProvider.Example.Model.Customer Dataset_ModelRecords_EqualityWorks_8() =>
        new Nimblesite.DataProvider.Example.Model.Customer(
            Id: 1,
            CustomerName: "Test",
            Email: null,
            Phone: null,
            CreatedDate: "2024-01-01",
            Addresses: []
        );

    private static global::Nimblesite.DataProvider.Example.Model.BasicOrder Dataset_ModelRecords_EqualityWorks_9() =>
        new Nimblesite.DataProvider.Example.Model.BasicOrder(
            "ORD-001",
            100.0,
            "Completed",
            "Corp",
            "email@test.com"
        );

    private static global::Nimblesite.DataProvider.Example.Model.BasicOrder Dataset_ModelRecords_EqualityWorks_10() =>
        new Nimblesite.DataProvider.Example.Model.BasicOrder(
            "ORD-001",
            100.0,
            "Completed",
            "Corp",
            "email@test.com"
        );
}
