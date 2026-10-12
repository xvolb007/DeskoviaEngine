using Deskovia.Application.Services;

namespace Deskovia.WebApi.Workers;

public sealed class PrototypeInitializer(IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OrganizationService>()
            .EnsureInitializedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
