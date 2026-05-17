using Microsoft.EntityFrameworkCore;
using SiteDiary.Domain.Entities;

namespace SiteDiary.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<ConstructionSite> ConstructionSites => Set<ConstructionSite>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Diary> Diaries => Set<Diary>();
    public DbSet<SiteUser> SiteUsers => Set<SiteUser>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<DiaryTemplate> DiaryTemplates => Set<DiaryTemplate>();
    public DbSet<AuditHistory> AuditHistories => Set<AuditHistory>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Soft-delete global query filters ──────────────────────────────
        modelBuilder.Entity<ConstructionSite>().HasQueryFilter(e => !e.IsArchived);
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsArchived);
        modelBuilder.Entity<Diary>().HasQueryFilter(e => !e.IsArchived);
        modelBuilder.Entity<DiaryTemplate>().HasQueryFilter(e => !e.IsArchived);

        // ── ConstructionSite ──────────────────────────────────────────────
        modelBuilder.Entity<ConstructionSite>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Address).HasMaxLength(500).IsRequired();
        });

        // ── User ──────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(e =>
        {
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("IX_Users_Email");
        });

        // ── Role ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Role>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.Name).IsUnique();
        });

        // ── Diary ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Diary>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Date).HasColumnType("datetimeoffset");
            e.Property(x => x.FieldOverrides).HasColumnType("nvarchar(max)");
            e.Property(x => x.Payload).HasColumnType("nvarchar(max)");
            e.Property(x => x.TemplateSnapshot).HasColumnType("nvarchar(max)");
            e.HasIndex(x => x.ConstructionSiteId).HasDatabaseName("IX_Diaries_ConstructionSiteId");
            e.HasIndex(x => x.AuthorUserId).HasDatabaseName("IX_Diaries_AuthorUserId");
            e.HasIndex(x => x.Date).HasDatabaseName("IX_Diaries_Date");

            e.HasOne(x => x.ConstructionSite).WithMany(s => s.Diaries)
                .HasForeignKey(x => x.ConstructionSiteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Author).WithMany(u => u.AuthoredDiaries)
                .HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DiaryTemplate).WithMany(t => t.Diaries)
                .HasForeignKey(x => x.DiaryTemplateId).OnDelete(DeleteBehavior.SetNull);
        });

        // ── SiteUser ──────────────────────────────────────────────────────
        modelBuilder.Entity<SiteUser>(e =>
        {
            e.HasIndex(x => new { x.ConstructionSiteId, x.UserId })
                .HasDatabaseName("IX_SiteUsers_ConstructionSiteId_UserId");
            e.HasOne(x => x.ConstructionSite).WithMany(s => s.SiteUsers)
                .HasForeignKey(x => x.ConstructionSiteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany(u => u.SiteUsers)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedRole).WithMany(r => r.SiteUsers)
                .HasForeignKey(x => x.AssignedRoleId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── UserRole ──────────────────────────────────────────────────────
        modelBuilder.Entity<UserRole>(e =>
        {
            e.HasIndex(x => x.UserId).HasDatabaseName("IX_UserRoles_UserId");
            e.HasOne(x => x.User).WithMany(u => u.UserRoles)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Role).WithMany(r => r.UserRoles)
                .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── DiaryTemplate ─────────────────────────────────────────────────
        modelBuilder.Entity<DiaryTemplate>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Sections).HasColumnType("nvarchar(max)");
            e.HasOne(x => x.CreatedBy).WithMany()
                .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Role)
             .WithMany(r => r.DiaryTemplates)
             .HasForeignKey(x => x.RoleId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => x.RoleId)
             .HasDatabaseName("IX_DiaryTemplates_RoleId");
        });

        // ── AuditHistory ──────────────────────────────────────────────────
        modelBuilder.Entity<AuditHistory>(e =>
        {
            e.Property(x => x.EntityName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Action).HasMaxLength(50).IsRequired();
            e.Property(x => x.Changes).HasColumnType("nvarchar(max)");
            e.HasIndex(x => new { x.EntityName, x.EntityId })
                .HasDatabaseName("IX_AuditHistories_EntityName_EntityId");
            e.HasIndex(x => x.ChangedByUserId).HasDatabaseName("IX_AuditHistories_ChangedByUserId");
            e.HasIndex(x => x.Timestamp).HasDatabaseName("IX_AuditHistories_Timestamp");
            e.HasOne(x => x.ChangedBy).WithMany()
                .HasForeignKey(x => x.ChangedByUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Attachment ────────────────────────────────────────────────────
        modelBuilder.Entity<Attachment>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.FileUrl).HasMaxLength(2048).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            e.Property(x => x.StorageProvider).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.DiaryId).HasDatabaseName("IX_Attachments_DiaryId");
            e.HasOne(x => x.Diary).WithMany(d => d.Attachments)
                .HasForeignKey(x => x.DiaryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.UploadedBy).WithMany()
                .HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
