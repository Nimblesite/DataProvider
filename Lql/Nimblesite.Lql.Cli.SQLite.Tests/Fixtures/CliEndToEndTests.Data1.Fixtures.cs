// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.Lql.Cli.SQLite.Tests;

public partial class CliEndToEndTests
{
    private const string Data_TranspileWithFilter_ToConsole_ReturnsCorrectSQL_1 = """
        employees
        |> select(employees.id, employees.name, employees.salary)
        |> filter(fn(row) => row.employees.salary > 50000 and row.employees.salary < 100000)
        """;

    private const string Data_TranspileAggregation_ToConsole_ReturnsCorrectSQL_1 = """
        orders
        |> select(orders.customer_id, COUNT(*) as order_count, SUM(orders.total) as total_amount)
        |> group_by(orders.customer_id)
        """;
}
