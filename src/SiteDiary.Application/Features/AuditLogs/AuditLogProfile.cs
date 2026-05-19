using AutoMapper;
using SiteDiary.Domain.Entities;

namespace SiteDiary.Application.Features.AuditLogs;

public sealed class AuditLogProfile : Profile
{
    public AuditLogProfile()
    {
        CreateMap<AuditHistory, AuditLogDto>()
            .ForCtorParam(nameof(AuditLogDto.ChangedByUserName), opt => opt.MapFrom(src =>
                src.ChangedBy != null
                    ? $"{src.ChangedBy.FirstName} {src.ChangedBy.LastName}"
                    : "System"));
    }
}
