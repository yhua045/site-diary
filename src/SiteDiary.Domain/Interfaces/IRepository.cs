namespace SiteDiary.Domain.Interfaces;

public interface IRepository<T> where T : class
{
    /// <summary>Returns an IQueryable for composing site-scoped, filtered, and eager-loaded queries.</summary>
    IQueryable<T> Query();
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}
