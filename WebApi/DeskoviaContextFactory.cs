using Deskovia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Deskovia.WebApi;

// Migration tooling reads local configuration without starting ABC initialisation or workers.
public sealed class DeskoviaContextFactory : IDesignTimeDbContextFactory<DeskoviaDbContext>
{
    public DeskoviaDbContext CreateDbContext(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var webApiDirectory = Path.Combine(currentDirectory, "WebApi");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(DeskoviaContextFactory).Assembly.GetName().Name,
            ContentRootPath = File.Exists(Path.Combine(webApiDirectory, "WebApi.csproj"))
                ? webApiDirectory : currentDirectory
        });
        // Package Manager Console may use Production; still load the developer's local secret.
        // Restore environment/command-line precedence after adding User Secrets explicitly.
        builder.Configuration.AddUserSecrets<DeskoviaContextFactory>(optional: true)
            .AddEnvironmentVariables().AddCommandLine(args);
        var options = new DbContextOptionsBuilder<DeskoviaDbContext>();
        SqlServerDatabase.Configure(options, SqlServerDatabase.ConnectionString(builder.Configuration));
        return new DeskoviaDbContext(options.Options);
    }
}
