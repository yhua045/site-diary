using Microsoft.AspNetCore.Mvc;
using AutoMapper;
using SiteDiary.Application.Features.AuditLogs;
using SiteDiary.Web.Middleware;

namespace SiteDiary.Web.Features.AuditLogs;

public class AuditLogsController(IAuditLogService auditLogService, IMapper mapper) : Controller
{
    [HttpGet]
    [SkipResourceAuthorization]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var result = await auditLogService.GetPageAsync(page, pageSize, ct);
        var dto = new AuditLogPageDto(
            mapper.Map<IReadOnlyList<AuditLogDto>>(result.Items),
            result.TotalCount,
            result.Page,
            result.PageSize);

        return View(dto);
    }
}
