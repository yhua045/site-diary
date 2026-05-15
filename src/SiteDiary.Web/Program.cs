using Microsoft.EntityFrameworkCore;
using SiteDiary.Application.Features.Attachments;
using SiteDiary.Application.Features.Diaries;
using SiteDiary.Application.Interfaces;
using SiteDiary.Application.Services;
using SiteDiary.Domain.Interfaces;
using SiteDiary.Infrastructure.Data;
using SiteDiary.Infrastructure.Repositories;
using SiteDiary.Infrastructure.Services;
using SiteDiary.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.MigrationsAssembly("SiteDiary.Infrastructure")));

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

// ── MVC + OpenAPI ─────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ── CORS for React dev server ─────────────────────────────────────────────────
builder.Services.AddCors(o =>
    o.AddPolicy("DevFrontend", p =>
        p.WithOrigins("http://localhost:5173")
         .AllowAnyHeader()
         .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("DevFrontend");
}

app.UseHttpsRedirection();

// ── Middleware ────────────────────────────────────────────────────────────────
app.UseMiddleware<XUserIdMiddleware>();  // before MapControllers
app.MapControllers();

// Serve React build from wwwroot (production)
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

// ── Adapter for IWebHostEnvironmentInfo ───────────────────────────────────────
public class WebHostEnvironmentAdapter(IWebHostEnvironment env) : IWebHostEnvironmentInfo
{
    public string ContentRootPath => env.ContentRootPath;
}

// Required for WebApplicationFactory in integration tests
public partial class Program { }
