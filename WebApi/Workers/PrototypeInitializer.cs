using Deskovia.Application.Services;
using Deskovia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Deskovia.WebApi.Workers;

public sealed class PrototypeInitializer(IServiceScopeFactory scopes, ILogger<PrototypeInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DeskoviaDbContext>();
        logger.LogInformation("Applying pending database migrations before starting ABC.");
        await database.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database migrations are up to date.");
        await scope.ServiceProvider.GetRequiredService<OrganizationService>()
            .EnsureInitializedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
