using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SiteDiary.Application.Features.Attachments;
using SiteDiary.Application.Features.AuditLogs;
using SiteDiary.Application.Features.Diaries;
using SiteDiary.Application.Features.DiaryTemplates;
using SiteDiary.Application.Interfaces;
using SiteDiary.Application.Services;
using SiteDiary.Domain.Interfaces;
using SiteDiary.Infrastructure.Data;
using SiteDiary.Infrastructure.Repositories;
using SiteDiary.Infrastructure.Services;
using SiteDiary.Infrastructure.Interceptors;
using SiteDiary.Web.Middleware;
using SiteDiary.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Only attempt to ensure DB exists outside of test environments
if (!builder.Environment.IsEnvironment("Testing"))
{
    await EnsureDatabaseExistsAsync(builder.Configuration.GetConnectionString("DefaultConnection"));
}

// ── Audit Infrastructure (Issue #7) ─────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

// ── Database ──────────────────────────────────────────────────────────────────
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sql => sql.MigrationsAssembly("SiteDiary.Infrastructure"))
               .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
}

// ── Repositories & Unit of Work ───────────────────────────────────────────────
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ── Domain Services ───────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IStorageService, LocalStorageService>();
builder.Services.AddSingleton<IWebHostEnvironmentInfo>(sp =>
    new WebHostEnvironmentAdapter(sp.GetRequiredService<IWebHostEnvironment>()));

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<ISiteService, SiteService>();
builder.Services.AddScoped<IUserService, UserService>();

// ── Feature Services (Vertical Slice — Issue #2) ──────────────────────────────
builder.Services.AddScoped<IDiaryService, DiaryService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IDiaryTemplateService, DiaryTemplateService>();

// ── Audit Log Service (Issue #7) ──────────────────────────────────────────────
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// ── Security Context & Authorization ─────────────────────────────────────────
builder.Services.AddScoped<IRequestSecurityContext, RequestSecurityContext>();
builder.Services.AddScoped<ISiteAuthorizationService, SiteAuthorizationService>();

// ── MVC + OpenAPI ─────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddControllersWithViews();  // Enable Razor views for MVC controllers
builder.Services.AddOpenApi();

// ── CORS for React dev server ─────────────────────────────────────────────────
builder.Services.AddCors(o =>
    o.AddPolicy("DevFrontend", p =>
        p.WithOrigins("http://localhost:5173")
         .AllowAnyHeader()
         .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Docker")
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await db.SeedAsync();
    }

    app.MapOpenApi();
    app.UseCors("DevFrontend");
}

app.UseHttpsRedirection();

// ── Middleware ────────────────────────────────────────────────────────────────
app.UseRouting();                                          // explicit — ensures RouteValues populated
app.UseMiddleware<RequestContextExtractionMiddleware>();   // replaces XUserIdMiddleware
app.UseMiddleware<ResourceAuthorizationMiddleware>();      // enforces auth rules
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AuditLogs}/{action=Index}/{id?}");  // MVC controller routing for Audit Logs, etc.

// Serve React build from wwwroot (production)
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

static async Task EnsureDatabaseExistsAsync(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Connection string 'DefaultConnection' was not configured.");
    }

    var builder = new SqlConnectionStringBuilder(connectionString);
    var databaseName = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "SiteDiary" : builder.InitialCatalog;

    var masterBuilder = new SqlConnectionStringBuilder(connectionString)
    {
        InitialCatalog = "master"
    };

    await using var connection = new SqlConnection(masterBuilder.ConnectionString);
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = $"IF DB_ID(N'{databaseName.Replace("'", "''")}') IS NULL CREATE DATABASE [{databaseName.Replace("]", "]]" )}];";
    await command.ExecuteNonQueryAsync();
}

// ── Adapter for IWebHostEnvironmentInfo ───────────────────────────────────────
public class WebHostEnvironmentAdapter(IWebHostEnvironment env) : IWebHostEnvironmentInfo
{
    public string ContentRootPath => env.ContentRootPath;
}

// Required for WebApplicationFactory in integration tests
public partial class Program { }
