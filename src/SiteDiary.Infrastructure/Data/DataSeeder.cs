using Bogus;
using Microsoft.EntityFrameworkCore;
using SiteDiary.Domain.Entities;

namespace SiteDiary.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(this ApplicationDbContext context)
    {
        // Avoid double-seeding
        if (await context.ConstructionSites.AnyAsync() || 
            await context.Roles.AnyAsync() || 
            await context.Users.AnyAsync())
        {
            return;
        }

        // 1. Seed Construction Sites
        var siteFaker = new Faker<ConstructionSite>()
            .RuleFor(s => s.Name, f => f.Company.CompanyName() + " Site")
            .RuleFor(s => s.Description, f => f.Lorem.Sentence())
            .RuleFor(s => s.Address, f => f.Address.FullAddress())
            .RuleFor(s => s.CreatedAt, _ => DateTime.UtcNow);

        var sites = siteFaker.Generate(2);
        await context.ConstructionSites.AddRangeAsync(sites);

        // 2. Seed Roles
        var roleNames = new[] 
        { 
            "Project Manager", 
            "Site Manager", 
            "Safety Manager", 
            "Site Foreman", 
            "Construction Worker" 
        };

        var roles = roleNames.Select(name => new Role 
        { 
            Name = name,
            Description = $"{name} role",
            CreatedAt = DateTime.UtcNow
        }).ToList();
        
        await context.Roles.AddRangeAsync(roles);

        // Save changes here so roles have IDs for linking if we were mapping directly, 
        // but Entity Framework will handle the inserts if we don't save yet.
        await context.SaveChangesAsync(); 

        // 3. Seed Users
        var userFaker = new Faker<User>()
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.FirstName, u.LastName))
            .RuleFor(u => u.IsActive, _ => true)
            .RuleFor(u => u.IsArchived, _ => false)
            .RuleFor(u => u.CreatedAt, _ => DateTime.UtcNow);

        var users = new List<User>();

        foreach (var role in roles)
        {
            // Seed 1 user per role
            var roleUsers = userFaker.Generate(1);
            foreach (var user in roleUsers)
            {
                user.UserRoles.Add(new UserRole 
                { 
                    RoleId = role.Id, 
                    Role = role 
                });
                users.Add(user);
            }
        }

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();

        // 4. Seed a default DiaryTemplate (used by GetByUserRoleAsync POC)
        var defaultTemplate = new DiaryTemplate
        {
            Name = "Site Daily Report",
            IsDefault = true,
            CreatedByUserId = users[0].Id,
            Sections = """
                [
                  {
                    "id": "s1",
                    "label": "General",
                    "fields": [
                      { "id": "f_title_note", "label": "Notes", "type": "textarea", "required": false }
                    ]
                  },
                  {
                    "id": "s2",
                    "label": "Weather & Environment",
                    "fields": [
                      {
                        "id": "f_weather",
                        "label": "Weather",
                        "type": "select",
                        "required": true,
                        "options": ["Sunny", "Cloudy", "Rainy", "Stormy", "Windy"]
                      },
                      { "id": "f_temp", "label": "Temperature (°C)", "type": "number", "required": false, "min": -20, "max": 60 }
                    ]
                  },
                  {
                    "id": "s3",
                    "label": "Work Progress",
                    "fields": [
                      { "id": "f_activities", "label": "Activities Completed", "type": "textarea", "required": true, "placeholder": "Describe work completed today..." },
                      { "id": "f_incidents", "label": "Safety Incidents", "type": "checkbox", "required": false }
                    ]
                  }
                ]
                """,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await context.DiaryTemplates.AddAsync(defaultTemplate);
        await context.SaveChangesAsync();
    }
}
