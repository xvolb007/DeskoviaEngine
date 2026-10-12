using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Deskovia.Infrastructure.Persistence;

public static class SqlServerDatabase
{
    public const string ConnectionName = "DeskoviaSqlServer";

    public static string ConnectionString(IConfiguration configuration)
    {
        var value = configuration.GetConnectionString(ConnectionName);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Configure ConnectionStrings:DeskoviaSqlServer in WebApi/appsettings.json or protected deployment configuration.");
        try
        {
            var parsed = new SqlConnectionStringBuilder(value);
            if (string.IsNullOrWhiteSpace(parsed.DataSource) || string.IsNullOrWhiteSpace(parsed.InitialCatalog))
                throw new ArgumentException();
            return parsed.ConnectionString;
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("ConnectionStrings:DeskoviaSqlServer must specify a SQL Server instance and database.");
        }
    }

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseSqlServer(connectionString);
}
