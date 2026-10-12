using Deskovia.Application.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Deskovia.Infrastructure.Identity;

public sealed class LocalPasswordService : IPasswordService
{
    private readonly PasswordHasher<object> hasher = new();
    private readonly object user = new();
    public string Hash(string password) => hasher.HashPassword(user, password);
    public bool Verify(string hash, string password, out bool rehash)
    {
        var result = hasher.VerifyHashedPassword(user, hash, password);
        rehash = result == PasswordVerificationResult.SuccessRehashNeeded;
        return result != PasswordVerificationResult.Failed;
    }
}
public sealed class DemoDirectoryAdapter : IDirectoryAdapter
{
    public Task<string> GetStateAsync(string configuredState, CancellationToken ct) => Task.FromResult(configuredState);
}
