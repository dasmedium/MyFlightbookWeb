using MySql.Data.MySqlClient;

namespace MyFlightbook.Data.IntegrationTests;

[TestClass]
public class DatabaseSmokeTests
{
    private const string ConnectionStringVariable = "ConnectionStrings__logbookConnectionString";

    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable);

    [TestMethod]
    public void DatabaseIsReachableAndCoreTablesExist()
    {
        string? connectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Inconclusive($"Set {ConnectionStringVariable} to run database integration tests.");
            return;
        }

        DBHelper.Init(connectionString);

        List<string> tables = [];
        DBHelper dbh = new("SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE()");
        dbh.ReadRows(
            (comm) => { },
            (dr) => tables.Add(dr.GetString(0)));

        Assert.IsTrue(tables.Count >= 50, $"Expected at least 50 tables, found {tables.Count}.");

        foreach (string table in new[] { "users", "flights", "aircraft", "flightproperties", "endorsements", "flighttelemetry", "images", "localconfig" })
        {
            CollectionAssert.Contains(tables, table, $"Missing table: {table}");
        }
    }

    [TestMethod]
    public void DatabaseCanExecuteScalarQuery()
    {
        string? connectionString = ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Inconclusive($"Set {ConnectionStringVariable} to run database integration tests.");
            return;
        }

        DBHelper.Init(connectionString);

        int result = 0;
        DBHelper dbh = new("SELECT 1");
        dbh.ReadRow((comm) => { }, (dr) => result = dr.GetInt32(0));

        Assert.AreEqual(1, result);
    }
}
