using Donation.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace Donation.Infrastructure.Repositories;

public class BaseRepository<TEntity>(AppDbContext db) : IBaseRepository<TEntity> where TEntity : class
{
    protected readonly AppDbContext _db = db;
    protected readonly DbSet<TEntity> _set = db.Set<TEntity>();

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _set.FindAsync([id], ct);
    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await _set.AsNoTracking().ToListAsync(ct);
    }

    public virtual IQueryable<TEntity> Query() => _set.AsQueryable();

    public virtual async Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await _set.AddAsync(entity, ct);
    }

    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        await _set.AddRangeAsync(entities, ct);
    }

    public virtual void Update(TEntity entity)
    {
        _set.Update(entity);
    }

    public virtual void Remove(TEntity entity)
    {
        _set.Remove(entity);
    }

    public virtual void RemoveRange(IEnumerable<TEntity> entities)
    {
        _set.RemoveRange(entities);
    }

    public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _set.FindAsync([id], ct);
        return entity != null;
    }

    public virtual Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _db.SaveChangesAsync(ct);
    }
}
