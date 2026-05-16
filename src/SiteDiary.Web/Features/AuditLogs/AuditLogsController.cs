using Microsoft.AspNetCore.Mvc;
using SiteDiary.Application.Features.AuditLogs;

namespace SiteDiary.Web.Features.AuditLogs;

public class AuditLogsController(IAuditLogService auditLogService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var result = await auditLogService.GetPageAsync(page, pageSize, ct);
        return View(result);
    }
}
