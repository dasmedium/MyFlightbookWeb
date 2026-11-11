using MyFlightbook;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string? connectionString = builder.Configuration.GetConnectionString("logbookConnectionString");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'logbookConnectionString' is required. Set ConnectionStrings__logbookConnectionString.");
}

DBHelper.Init(connectionString);

WebApplication app = builder.Build();

app.MapGet("/", () => Results.Text("MyFlightbook modern host"));

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapGet("/healthz/db", IResult () =>
{
    try
    {
        int result = 0;
        DBHelper dbh = new DBHelper("SELECT 1");
        dbh.ReadRow((comm) => { }, (dr) => result = dr.GetInt32(0));

        return result == 1
            ? Results.Ok(new { status = "ok", database = "ok" })
            : Results.Problem("Database check returned an unexpected value.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database health check failed.");
        return Results.Problem("Database health check failed.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();
