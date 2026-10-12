using Deskovia.Application.Interfaces;
using Deskovia.Application.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Deskovia.Infrastructure.Persistence;

public sealed class WorkspaceStore(DeskoviaDbContext db) : IWorkspaceStore
{
    private IDbContextTransaction? transaction;
    public IQueryable<T> Query<T>() where T : class => db.Set<T>();
    public void Add<T>(T entity) where T : class => db.Add(entity);
    public void Remove<T>(T entity) where T : class => db.Remove(entity);
    public async Task<IAsyncDisposable> LockAsync(CancellationToken ct = default)
    {
        transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // The lock is shared by all application instances in this database and released by commit/rollback.
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = N'Deskovia:ABC:mutations:v1',
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 15000;
                IF @result < 0
                    THROW 51001, 'Unable to acquire the workspace transaction lock.', 1;
                """, ct);
            db.ChangeTracker.Clear();
            return transaction;
        }
        catch (Exception e)
        {
            await transaction.DisposeAsync();
            transaction = null;
            if (e is SqlException { Number: 51001 or 1205 })
                throw new BusinessException(409, "workspace_busy", "The workspace is busy. Try again.");
            throw;
        }
    }
    public async Task CommitAsync(CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        transaction = null;
    }
}
