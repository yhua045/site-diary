using Bogus;
using Microsoft.EntityFrameworkCore;
using SiteDiary.Domain.Entities;

namespace SiteDiary.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(this ApplicationDbContext context)
    {
        // Avoid double-seeding (check all seeded tables)
        if (await context.ConstructionSites.AnyAsync() ||
            await context.Roles.AnyAsync()             ||
            await context.Users.AnyAsync()             ||
            await context.DiaryTemplates.AnyAsync())
        {
            return;
        }

        // ── Step 1: Seed Construction Sites ──────────────────────────────────
        var siteFaker = new Faker<ConstructionSite>()
            .RuleFor(s => s.Name, f => f.Company.CompanyName() + " Site")
            .RuleFor(s => s.Description, f => f.Lorem.Sentence())
            .RuleFor(s => s.Address, f => f.Address.FullAddress())
            .RuleFor(s => s.CreatedAt, _ => DateTime.UtcNow);

        var sites = siteFaker.Generate(2);
        await context.ConstructionSites.AddRangeAsync(sites);

        // ── Step 2: Seed Roles ────────────────────────────────────────────────
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

        // ── Step 3: SaveChanges so roles get IDs ──────────────────────────────
        await context.SaveChangesAsync();

        // ── Step 4: Seed Users (1 per role) ──────────────────────────────────
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
            var user = userFaker.Generate();
            user.UserRoles.Add(new UserRole
            {
                RoleId = role.Id,
                Role = role
            });
            users.Add(user);
        }

        await context.Users.AddRangeAsync(users);

        // ── Step 5: SaveChanges so users get IDs ─────────────────────────────
        await context.SaveChangesAsync();

        // ── Step 6: Seed SiteUser associations ───────────────────────────────
        var siteUsers = new List<SiteUser>();
        for (int i = 0; i < users.Count; i++)
        {
            var user = users[i];
            var site = i < 2 ? sites[0] : sites[1];
            var primaryRole = user.UserRoles.First().RoleId;

            siteUsers.Add(new SiteUser
            {
                ConstructionSiteId = site.Id,
                UserId = user.Id,
                AssignedRoleId = primaryRole,
                JoinedDate = DateOnly.FromDateTime(DateTime.UtcNow)
            });
        }
        await context.Set<SiteUser>().AddRangeAsync(siteUsers);

        // ── Step 7: SaveChanges ───────────────────────────────────────────────
        await context.SaveChangesAsync();

        // ── Step 8: Seed role-specific DiaryTemplates ─────────────────────────
        // Map role name → Role entity for FK assignment
        var rolesMap = roles.ToDictionary(r => r.Name);

        var roleTemplates = new List<DiaryTemplate>
        {
            // 4.1 Project Manager
            new()
            {
                Name = "Project Manager Daily Report",
                IsDefault = false,
                CreatedByUserId = users[0].Id,
                RoleId = rolesMap["Project Manager"].Id,
                Sections = """
                    [
                      {
                        "id": "s1",
                        "label": "Progress",
                        "fields": [
                          {
                            "id": "f_progress_summary",
                            "label": "Progress Summary",
                            "type": "textarea",
                            "required": true,
                            "placeholder": "Describe overall site progress today..."
                          }
                        ]
                      },
                      {
                        "id": "s2",
                        "label": "Issues & Actions",
                        "fields": [
                          {
                            "id": "f_blockers",
                            "label": "Blockers / Risks",
                            "type": "textarea",
                            "required": false,
                            "placeholder": "List any blockers or risks..."
                          },
                          {
                            "id": "f_actions",
                            "label": "Actions / Next Steps",
                            "type": "textarea",
                            "required": false,
                            "placeholder": "What actions are planned?"
                          }
                        ]
                      },
                      {
                        "id": "s3",
                        "label": "Notes",
                        "fields": [
                          {
                            "id": "f_notes",
                            "label": "Notes / Comments",
                            "type": "textarea",
                            "required": false
                          }
                        ]
                      },
                      {
                        "id": "s_attachments",
                        "label": "Attachments",
                        "fields": [
                          {
                            "id": "f_file_attachment",
                            "label": "File Attachments",
                            "type": "file_attachment",
                            "required": false,
                            "placeholder": "Attach photos, documents or other files..."
                          },
                          {
                            "id": "f_dynamic_fields",
                            "label": "Custom Fields",
                            "type": "dynamic_fields",
                            "required": false
                          }
                        ]
                      }
                    ]
                    """,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },

            // 4.2 Site Manager
            new()
            {
                Name = "Site Manager Daily Report",
                IsDefault = false,
                CreatedByUserId = users[0].Id,
                RoleId = rolesMap["Site Manager"].Id,
                Sections = """
                    [
                      {
                        "id": "s1",
                        "label": "Work",
                        "fields": [
                          {
                            "id": "f_work_completed",
                            "label": "Work Completed Today",
                            "type": "textarea",
                            "required": true,
                            "placeholder": "Describe work completed..."
                          },
                          {
                            "id": "f_crew_count",
                            "label": "Crew / Manpower Count",
                            "type": "number",
                            "required": true,
                            "min": 0,
                            "max": 500
                          }
                        ]
                      },
                      {
                        "id": "s2",
                        "label": "Conditions & Issues",
                        "fields": [
                          {
                            "id": "f_site_conditions",
                            "label": "Site Conditions",
                            "type": "textarea",
                            "required": false,
                            "placeholder": "Weather, ground conditions, access..."
                          },
                          {
                            "id": "f_issues",
                            "label": "Issues / Blockers",
                            "type": "textarea",
                            "required": false
                          }
                        ]
                      },
                      {
                        "id": "s3",
                        "label": "Planning",
                        "fields": [
                          {
                            "id": "f_next_plan",
                            "label": "Next Plan",
                            "type": "textarea",
                            "required": false,
                            "placeholder": "What is planned for tomorrow?"
                          }
                        ]
                      },
                      {
                        "id": "s_attachments",
                        "label": "Attachments",
                        "fields": [
                          {
                            "id": "f_file_attachment",
                            "label": "File Attachments",
                            "type": "file_attachment",
                            "required": false,
                            "placeholder": "Attach photos, documents or other files..."
                          },
                          {
                            "id": "f_dynamic_fields",
                            "label": "Custom Fields",
                            "type": "dynamic_fields",
                            "required": false
                          }
                        ]
                      }
                    ]
                    """,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },

            // 4.3 Safety Manager
            new()
            {
                Name = "Safety Manager Daily Report",
                IsDefault = false,
                CreatedByUserId = users[0].Id,
                RoleId = rolesMap["Safety Manager"].Id,
                Sections = """
                    [
                      {
                        "id": "s1",
                        "label": "Compliance",
                        "fields": [
                          {
                            "id": "f_safety_check",
                            "label": "Safety Check / Compliance",
                            "type": "textarea",
                            "required": true,
                            "placeholder": "Describe safety checks performed today..."
                          },
                          {
                            "id": "f_hazards",
                            "label": "Hazards Observed",
                            "type": "textarea",
                            "required": false
                          }
                        ]
                      },
                      {
                        "id": "s2",
                        "label": "Incidents",
                        "fields": [
                          {
                            "id": "f_incidents",
                            "label": "Incidents / Near Misses",
                            "type": "textarea",
                            "required": false,
                            "placeholder": "Any incidents or near misses to report?"
                          },
                          {
                            "id": "f_corrective_actions",
                            "label": "Corrective Actions",
                            "type": "textarea",
                            "required": false
                          }
                        ]
                      },
                      {
                        "id": "s3",
                        "label": "Follow-up",
                        "fields": [
                          {
                            "id": "f_followup_owner",
                            "label": "Follow-up Owner / Due Date",
                            "type": "text",
                            "required": false,
                            "placeholder": "Name and due date for follow-up..."
                          }
                        ]
                      },
                      {
                        "id": "s_attachments",
                        "label": "Attachments",
                        "fields": [
                          {
                            "id": "f_file_attachment",
                            "label": "File Attachments",
                            "type": "file_attachment",
                            "required": false,
                            "placeholder": "Attach photos, documents or other files..."
                          },
                          {
                            "id": "f_dynamic_fields",
                            "label": "Custom Fields",
                            "type": "dynamic_fields",
                            "required": false
                          }
                        ]
                      }
                    ]
                    """,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },

            // 4.4 Site Foreman
            new()
            {
                Name = "Site Foreman Daily Report",
                IsDefault = false,
                CreatedByUserId = users[0].Id,
                RoleId = rolesMap["Site Foreman"].Id,
                Sections = """
                    [
                      {
                        "id": "s1",
                        "label": "Tasks",
                        "fields": [
                          {
                            "id": "f_tasks_completed",
                            "label": "Tasks Completed",
                            "type": "textarea",
                            "required": true,
                            "placeholder": "List tasks completed today..."
                          },
                          {
                            "id": "f_work_in_progress",
                            "label": "Work in Progress",
                            "type": "textarea",
                            "required": false
                          }
                        ]
                      },
                      {
                        "id": "s2",
                        "label": "Resources & Blockers",
                        "fields": [
                          {
                            "id": "f_resource_notes",
                            "label": "Resource / Manpower Notes",
                            "type": "textarea",
                            "required": false
                          },
                          {
                            "id": "f_blockers",
                            "label": "Blockers",
                            "type": "textarea",
                            "required": false
                          }
                        ]
                      },
                      {
                        "id": "s3",
                        "label": "Planning",
                        "fields": [
                          {
                            "id": "f_next_day_plan",
                            "label": "Next-Day Plan",
                            "type": "textarea",
                            "required": false,
                            "placeholder": "What is planned for tomorrow?"
                          }
                        ]
                      },
                      {
                        "id": "s_attachments",
                        "label": "Attachments",
                        "fields": [
                          {
                            "id": "f_file_attachment",
                            "label": "File Attachments",
                            "type": "file_attachment",
                            "required": false,
                            "placeholder": "Attach photos, documents or other files..."
                          },
                          {
                            "id": "f_dynamic_fields",
                            "label": "Custom Fields",
                            "type": "dynamic_fields",
                            "required": false
                          }
                        ]
                      }
                    ]
                    """,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },

            // 4.5 Construction Worker
            new()
            {
                Name = "Construction Worker Daily Report",
                IsDefault = false,
                CreatedByUserId = users[0].Id,
                RoleId = rolesMap["Construction Worker"].Id,
                Sections = """
                    [
                      {
                        "id": "s1",
                        "label": "Work Done",
                        "fields": [
                          {
                            "id": "f_tasks_performed",
                            "label": "Tasks Performed",
                            "type": "textarea",
                            "required": true,
                            "placeholder": "What did you work on today?"
                          },
                          {
                            "id": "f_tools_equipment",
                            "label": "Tools / Equipment Used",
                            "type": "textarea",
                            "required": false
                          }
                        ]
                      },
                      {
                        "id": "s2",
                        "label": "Issues",
                        "fields": [
                          {
                            "id": "f_issues",
                            "label": "Issues / Blockers",
                            "type": "textarea",
                            "required": false
                          },
                          {
                            "id": "f_notes",
                            "label": "Notes",
                            "type": "textarea",
                            "required": false
                          }
                        ]
                      },
                      {
                        "id": "s_attachments",
                        "label": "Attachments",
                        "fields": [
                          {
                            "id": "f_file_attachment",
                            "label": "File Attachments",
                            "type": "file_attachment",
                            "required": false,
                            "placeholder": "Attach photos, documents or other files..."
                          },
                          {
                            "id": "f_dynamic_fields",
                            "label": "Custom Fields",
                            "type": "dynamic_fields",
                            "required": false
                          }
                        ]
                      }
                    ]
                    """,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };

        await context.DiaryTemplates.AddRangeAsync(roleTemplates);

        // 4.6 System fallback template (IsDefault=true, RoleId=null)
        // Updated to include Attachments section (R9, R10).
        var fallbackTemplate = new DiaryTemplate
        {
            Name = "Site Daily Report",
            IsDefault = true,
            CreatedByUserId = users[0].Id,
            RoleId = null,
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
                      { "id": "f_temp", "label": "Temperature (\u00b0C)", "type": "number", "required": false, "min": -20, "max": 60 }
                    ]
                  },
                  {
                    "id": "s3",
                    "label": "Work Progress",
                    "fields": [
                      { "id": "f_activities", "label": "Activities Completed", "type": "textarea", "required": true, "placeholder": "Describe work completed today..." },
                      { "id": "f_incidents", "label": "Safety Incidents", "type": "checkbox", "required": false }
                    ]
                  },
                  {
                    "id": "s_attachments",
                    "label": "Attachments",
                    "fields": [
                      {
                        "id": "f_file_attachment",
                        "label": "File Attachments",
                        "type": "file_attachment",
                        "required": false,
                        "placeholder": "Attach photos, documents or other files..."
                      },
                      {
                        "id": "f_dynamic_fields",
                        "label": "Custom Fields",
                        "type": "dynamic_fields",
                        "required": false
                      }
                    ]
                  }
                ]
                """,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await context.DiaryTemplates.AddAsync(fallbackTemplate);

        // ── Step 9: Final save ────────────────────────────────────────────────
        await context.SaveChangesAsync();
    }
}
