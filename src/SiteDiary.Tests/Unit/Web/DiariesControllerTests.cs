using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SiteDiary.Application.Features.Diaries;
using SiteDiary.Application.Shared;
using SiteDiary.Domain.Entities;
using SiteDiary.Web.Features.Diaries;
using SiteDiary.Web.Middleware;

namespace SiteDiary.Tests.Unit.Web;

public class DiariesControllerTests
{
    private readonly Mock<IDiaryService> _svc = new();

    private DiariesController MakeController(int? userId = 42)
    {
        var ctrl = new DiariesController(_svc.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        if (userId.HasValue)
        {
            ctrl.HttpContext.Items[HttpContextKeys.UserId] = userId.Value;
        }

        return ctrl;
    }

    private static Diary MakeDiary(int id = 1, int siteId = 10, DateTimeOffset? date = null) => new()
    {
        Id = id,
        ConstructionSiteId = siteId,
        AuthorUserId = 42,
        Title = $"Diary {id}",
        Content = "Body",
        Date = date ?? new DateTimeOffset(2026, 5, 17, 14, 30, 0, TimeSpan.FromHours(2)),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetAll_ReturnsMappedDtosWithDateTimeOffsetDates()
    {
        var entityDate = new DateTimeOffset(2026, 5, 17, 14, 30, 0, TimeSpan.FromHours(2));
        var entities = new List<Diary> { MakeDiary(1, date: entityDate), MakeDiary(2, date: entityDate.AddDays(-1)) };

        _svc.Setup(s => s.GetBySiteIdAsync(10, default)).ReturnsAsync(entities);

        var result = await MakeController().GetAll(10, default);

        result.Result.Should().BeOfType<OkObjectResult>();
        var value = (List<DiaryDto>)((OkObjectResult)result.Result!).Value!;
        value.Should().HaveCount(2);
        value[0].Date.Should().Be(entityDate);
        value[1].Date.Should().Be(entityDate.AddDays(-1));
    }

    [Fact]
    public async Task GetTimeline_ReturnsMappedTimelineDtosWithDateTimeOffsetDates()
    {
        var entityDate = new DateTimeOffset(2026, 5, 17, 14, 30, 0, TimeSpan.FromHours(2));
        var entities = new List<Diary> { MakeDiary(1, date: entityDate) };

        _svc.Setup(s => s.GetTimelineAsync(10, default)).ReturnsAsync(entities);

        var result = await MakeController().GetTimeline(10, default);

        result.Result.Should().BeOfType<OkObjectResult>();
        var value = (List<DiaryTimelineEntryDto>)((OkObjectResult)result.Result!).Value!;
        value.Should().HaveCount(1);
        value[0].Date.Should().Be(entityDate);
    }

    [Fact]
    public async Task GetById_ExistingDiary_Returns200WithMappedDto()
    {
        var entityDate = new DateTimeOffset(2026, 5, 17, 14, 30, 0, TimeSpan.FromHours(2));
        var entity = MakeDiary(1, date: entityDate);

        _svc.Setup(s => s.GetByIdWithAttachmentsAsync(10, 1, default)).ReturnsAsync(entity);

        var result = await MakeController().GetById(10, 1, default);

        result.Result.Should().BeOfType<OkObjectResult>();
        var value = (DiaryDetailDto)((OkObjectResult)result.Result!).Value!;
        value.Date.Should().Be(entityDate);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        _svc.Setup(s => s.GetByIdWithAttachmentsAsync(10, 99, default)).ReturnsAsync((Diary?)null);

        var result = await MakeController().GetById(10, 99, default);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Create_MapsDateAndCallsServiceWithExactOffset()
    {
        var dtoDate = new DateTimeOffset(2026, 5, 16, 10, 15, 0, TimeSpan.FromHours(-5));
        var dto = new CreateDiaryDto("Title", null, dtoDate);
        Diary? captured = null;
        var saved = MakeDiary(5, date: dtoDate);

        _svc.Setup(s => s.CreateAsync(10, 42, It.IsAny<Diary>(), default))
            .Callback<int, int, Diary, CancellationToken>((_, _, diary, _) => captured = diary)
            .ReturnsAsync(saved);

        var result = await MakeController().Create(10, dto, default);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        captured.Should().NotBeNull();
        captured!.Date.Should().Be(dtoDate);
    }

    [Fact]
    public async Task Update_Success_Returns200WithMappedDto()
    {
        var dtoDate = new DateTimeOffset(2026, 5, 18, 8, 0, 0, TimeSpan.FromHours(1));
        var updateDto = new UpdateDiaryDto("New Title", null, dtoDate);
        Diary? captured = null;
        var updatedEntity = MakeDiary(1, date: dtoDate);

        _svc.Setup(s => s.UpdateAsync(10, 1, 42, It.IsAny<Diary>(), default))
            .Callback<int, int, int, Diary, CancellationToken>((_, _, _, diary, _) => captured = diary)
            .ReturnsAsync(OperationResult<Diary>.Ok(updatedEntity));

        var result = await MakeController().Update(10, 1, updateDto, default);

        result.Result.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!.Date.Should().Be(dtoDate);
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        var updateDto = new UpdateDiaryDto("X", null, DateTimeOffset.UtcNow);
        _svc.Setup(s => s.UpdateAsync(10, 99, 42, It.IsAny<Diary>(), default))
            .ReturnsAsync(OperationResult<Diary>.NotFound());

        var result = await MakeController().Update(10, 99, updateDto, default);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_Success_ReturnsNoContent()
    {
        _svc.Setup(s => s.DeleteAsync(10, 1, 42, default)).ReturnsAsync(OperationResult<bool>.Ok(true));

        var result = await MakeController().Delete(10, 1, default);

        result.Should().BeOfType<NoContentResult>();
    }
}
using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SiteDiary.Application.Features.Diaries;
using SiteDiary.Application.Shared;
using SiteDiary.Domain.Entities;
using SiteDiary.Web.Features.Diaries;

namespace SiteDiary.Tests.Unit.Web;

/// <summary>
/// Controller unit tests for DiariesController post-refactor (Issue #8).
/// Service mock returns domain entities; IMapper mock translates at boundary.
/// Phase B: these tests fail until the controller is updated in Phase C.
/// </summary>
public class DiariesControllerTests
{
    private readonly Mock<IDiaryService> _svc = new();
    private readonly Mock<IMapper> _mapper = new();

    private DiariesController MakeController()
    {
        var ctrl = new DiariesController(_svc.Object, _mapper.Object);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        ctrl.HttpContext.Items["CurrentUserId"] = 42;
        return ctrl;
    }

    private static Diary MakeDiary(int id = 1, int siteId = 10) => new()
    {
        Id = id,
        ConstructionSiteId = siteId,
        AuthorUserId = 42,
        Title = "Diary " + id,
        Date = DateOnly.FromDateTime(DateTime.UtcNow),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsMappedDtos()
    {
        var entities = new List<Diary> { MakeDiary(1), MakeDiary(2) };
        var dtos = new List<DiaryDto>
        {
            new(1, 10, 42, "Diary 1", null, DateTimeOffset.UtcNow, false),
            new(2, 10, 42, "Diary 2", null, DateTimeOffset.UtcNow, false)
        };

        _svc.Setup(s => s.GetBySiteIdAsync(10, default)).ReturnsAsync(entities);
        _mapper.Setup(m => m.Map<IReadOnlyList<DiaryDto>>(entities)).Returns(dtos);

        var result = await MakeController().GetAll(10, default);

        result.Result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result.Result!).Value.Should().BeSameAs(dtos);
        _mapper.Verify(m => m.Map<IReadOnlyList<DiaryDto>>(entities), Times.Once);
    }

    // ── GetTimeline ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTimeline_ReturnsMappedTimelineDtos()
    {
        var entities = new List<Diary> { MakeDiary(1) };
        var dtos = new List<DiaryTimelineEntryDto>
        {
            new(1, 10, 42, "Alice Smith", "Worker",
                DateOnly.FromDateTime(DateTime.UtcNow), true,
                System.Text.Json.JsonDocument.Parse("{}").RootElement,
                Array.Empty<FieldDescriptorDto>(),
                Array.Empty<AttachmentDto>())
        };

        _svc.Setup(s => s.GetTimelineAsync(10, default)).ReturnsAsync(entities);
        _mapper.Setup(m => m.Map<IReadOnlyList<DiaryTimelineEntryDto>>(entities)).Returns(dtos);

        var result = await MakeController().GetTimeline(10, default);

        result.Result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result.Result!).Value.Should().BeSameAs(dtos);
        _mapper.Verify(m => m.Map<IReadOnlyList<DiaryTimelineEntryDto>>(entities), Times.Once);
    }

    // ── GetById ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingDiary_Returns200WithMappedDto()
    {
        var entity = MakeDiary(1);
        var dto = new DiaryDetailDto(1, 10, 42, "Diary 1", null,
            DateTimeOffset.UtcNow, false, Array.Empty<AttachmentDto>());

        _svc.Setup(s => s.GetByIdWithAttachmentsAsync(10, 1, default)).ReturnsAsync(entity);
        _mapper.Setup(m => m.Map<DiaryDetailDto>(entity)).Returns(dto);

        var result = await MakeController().GetById(10, 1, default);

        result.Result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result.Result!).Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        _svc.Setup(s => s.GetByIdWithAttachmentsAsync(10, 99, default)).ReturnsAsync((Diary?)null);

        var result = await MakeController().GetById(10, 99, default);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_MapsInputThenCallsService_ThenMapsOutput()
    {
        var dto = new CreateDiaryDto("Title", null, DateTimeOffset.UtcNow);
        var domainDiary = MakeDiary(0);
        var savedDiary = MakeDiary(5);
        var responseDto = new DiaryDto(5, 10, 42, "Title", null, DateTimeOffset.UtcNow, false);

        _mapper.Setup(m => m.Map<Diary>(dto)).Returns(domainDiary);
        _svc.Setup(s => s.CreateAsync(10, 42, domainDiary, default)).ReturnsAsync(savedDiary);
        _mapper.Setup(m => m.Map<DiaryDto>(savedDiary)).Returns(responseDto);

        var result = await MakeController().Create(10, dto, default);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        _mapper.Verify(m => m.Map<Diary>(dto), Times.Once);
        _svc.Verify(s => s.CreateAsync(10, 42, domainDiary, default), Times.Once);
        _mapper.Verify(m => m.Map<DiaryDto>(savedDiary), Times.Once);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_Success_Returns200WithMappedDto()
    {
        var updateDto = new UpdateDiaryDto("New Title", null, DateTimeOffset.UtcNow);
        var updateValues = MakeDiary(0);
        var updatedEntity = MakeDiary(1);
        var responseDto = new DiaryDto(1, 10, 42, "New Title", null, DateTimeOffset.UtcNow, false);

        _mapper.Setup(m => m.Map<Diary>(updateDto)).Returns(updateValues);
        _svc.Setup(s => s.UpdateAsync(10, 1, 42, updateValues, default))
            .ReturnsAsync(OperationResult<Diary>.Ok(updatedEntity));
        _mapper.Setup(m => m.Map<DiaryDto>(updatedEntity)).Returns(responseDto);

        var result = await MakeController().Update(10, 1, updateDto, default);

        result.Result.Should().BeOfType<OkObjectResult>();
        _mapper.Verify(m => m.Map<Diary>(updateDto), Times.Once);
        _mapper.Verify(m => m.Map<DiaryDto>(updatedEntity), Times.Once);
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        var updateDto = new UpdateDiaryDto("X", null, DateTimeOffset.UtcNow);
        _mapper.Setup(m => m.Map<Diary>(updateDto)).Returns(MakeDiary(0));
        _svc.Setup(s => s.UpdateAsync(10, 99, 42, It.IsAny<Diary>(), default))
            .ReturnsAsync(OperationResult<Diary>.NotFound());

        var result = await MakeController().Update(10, 99, updateDto, default);

        result.Result.Should().BeOfType<NotFoundResult>();
    }
}
