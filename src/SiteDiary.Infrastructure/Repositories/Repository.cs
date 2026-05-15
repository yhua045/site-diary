using Microsoft.EntityFrameworkCore;
using SiteDiary.Domain.Interfaces;
using SiteDiary.Infrastructure.Data;

namespace SiteDiary.Infrastructure.Repositories;

public class Repository<T>(ApplicationDbContext db) : IRepository<T> where T : class
{
    protected readonly DbSet<T> _set = db.Set<T>();

    public IQueryable<T> Query() => _set.AsQueryable();

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _set.FindAsync([id], ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) =>
        await _set.AddAsync(entity, ct);

    public void Update(T entity) => _set.Update(entity);

    public void Remove(T entity) => _set.Remove(entity);
}
