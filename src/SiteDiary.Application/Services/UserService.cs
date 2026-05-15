using Microsoft.EntityFrameworkCore;
using SiteDiary.Application.DTOs;
using SiteDiary.Application.Interfaces;
using SiteDiary.Domain.Entities;
using SiteDiary.Domain.Interfaces;

namespace SiteDiary.Application.Services;

public class UserService(IUnitOfWork uow) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await uow.Users.Query().ToListAsync(ct);
        return users.Select(MapToDto).ToList();
    }

    public async Task<UserDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var user = await uow.Users.GetByIdAsync(id, ct);
        return user is null ? null : MapToDto(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await uow.Users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return MapToDto(user);
    }

    public async Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await uow.Users.GetByIdAsync(id, ct);
        if (user is null) return null;

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.Email = request.Email;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        uow.Users.Update(user);
        await uow.SaveChangesAsync(ct);
        return MapToDto(user);
    }

    private static UserDto MapToDto(User u) =>
        new(u.Id, u.FirstName, u.LastName, u.Email, u.IsActive, u.IsArchived, u.CreatedAt, u.UpdatedAt);
}
