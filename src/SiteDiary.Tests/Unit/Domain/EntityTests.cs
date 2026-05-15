using FluentAssertions;
using SiteDiary.Domain.Entities;

namespace SiteDiary.Tests.Unit.Domain;

/// <summary>
/// Tests domain entity defaults, computed properties, and constraints.
/// These are pure unit tests with no dependencies.
/// </summary>
public class EntityTests
{
    // ── BaseEntity ──────────────────────────────────────────────────────────
    [Fact]
    public void ConstructionSite_DefaultIsArchived_IsFalse()
    {
        var site = new ConstructionSite();
        site.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void User_DefaultIsActive_IsTrue()
    {
        var user = new User();
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void User_DefaultIsArchived_IsFalse()
    {
        var user = new User();
        user.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void User_FullName_ConcatenatesFirstAndLastName()
    {
        var user = new User { FirstName = "Jane", LastName = "Smith" };
        user.FullName.Should().Be("Jane Smith");
    }

    [Fact]
    public void Diary_DefaultIsPublished_IsFalse()
    {
        var diary = new Diary();
        diary.IsPublished.Should().BeFalse();
    }

    [Fact]
    public void Diary_DefaultIsArchived_IsFalse()
    {
        var diary = new Diary();
        diary.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void DiaryTemplate_DefaultSections_IsEmptyJsonArray()
    {
        var template = new DiaryTemplate();
        template.Sections.Should().Be("[]");
    }

    [Fact]
    public void DiaryTemplate_DefaultIsDefault_IsFalse()
    {
        var template = new DiaryTemplate();
        template.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void SiteUser_DefaultIsPrimaryContact_IsFalse()
    {
        var su = new SiteUser();
        su.IsPrimaryContact.Should().BeFalse();
    }

    [Fact]
    public void UserRole_DefaultIsActive_IsTrue()
    {
        var ur = new UserRole();
        ur.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Attachment_NavigationCollections_AreNotNull()
    {
        var diary = new Diary();
        diary.Attachments.Should().NotBeNull();
    }

    [Fact]
    public void ConstructionSite_Collections_AreInitialized()
    {
        var site = new ConstructionSite();
        site.SiteUsers.Should().NotBeNull();
        site.Diaries.Should().NotBeNull();
    }
}
