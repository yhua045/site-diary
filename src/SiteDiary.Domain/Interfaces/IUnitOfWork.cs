using SiteDiary.Domain.Entities;
using SiteDiary.Domain.Interfaces;

namespace SiteDiary.Domain.Interfaces;

public interface IUnitOfWork
{
    IRepository<ConstructionSite> Sites { get; }
    IRepository<User> Users { get; }
    IRepository<Diary> Diaries { get; }
    IRepository<DiaryTemplate> DiaryTemplates { get; }
    IRepository<Attachment> Attachments { get; }
    IRepository<AuditHistory> AuditHistories { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
