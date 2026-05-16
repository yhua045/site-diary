using SiteDiary.Domain.Entities;
using SiteDiary.Domain.Interfaces;
using SiteDiary.Infrastructure.Data;
using SiteDiary.Infrastructure.Repositories;

namespace SiteDiary.Infrastructure.Repositories;

public class UnitOfWork(ApplicationDbContext db) : IUnitOfWork
{
    public IRepository<ConstructionSite> Sites { get; } = new Repository<ConstructionSite>(db);
    public IRepository<User> Users { get; } = new Repository<User>(db);
    public IRepository<UserRole> UserRoles { get; } = new Repository<UserRole>(db);
    public IRepository<Diary> Diaries { get; } = new Repository<Diary>(db);
    public IRepository<DiaryTemplate> DiaryTemplates { get; } = new Repository<DiaryTemplate>(db);
    public IRepository<Attachment> Attachments { get; } = new Repository<Attachment>(db);
    public IRepository<AuditHistory> AuditHistories { get; } = new Repository<AuditHistory>(db);
    public IRepository<SiteUser> SiteUsers { get; } = new Repository<SiteUser>(db);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
