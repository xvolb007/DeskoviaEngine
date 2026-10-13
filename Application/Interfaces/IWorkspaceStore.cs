namespace Deskovia.Application.Interfaces;

public interface IWorkspaceStore
{
    IQueryable<T> Query<T>() where T : class;
    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    Task<IAsyncDisposable> LockAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
}
public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string hash, string password, out bool rehash);
}
public interface IDirectoryAdapter
{
    Task<string> GetStateAsync(string configuredState, CancellationToken ct);
}
public interface IEmailSink
{
    Task DeliverAsync(Guid id, string recipient, string subject, string body, CancellationToken ct);
}
