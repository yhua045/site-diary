using Microsoft.EntityFrameworkCore;
using SiteDiary.Application.DTOs;
using SiteDiary.Application.Interfaces;
using SiteDiary.Domain.Entities;
using SiteDiary.Domain.Interfaces;

namespace SiteDiary.Application.Services;

public class UserService(IUnitOfWork uow) : IUserService
{
    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await uow.Users.Query().ToListAsync(ct);
        return users;
    }

    public async Task<User?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var user = await uow.Users.GetByIdAsync(id, ct);
        return user;
    }

    public async Task<User> CreateAsync(User user, CancellationToken ct = default)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await uow.Users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return user;
    }

    public async Task<User?> UpdateAsync(int id, User updateValues, CancellationToken ct = default)
    {
        var user = await uow.Users.GetByIdAsync(id, ct);
        if (user is null) return null;

        user.FirstName = updateValues.FirstName;
        user.LastName = updateValues.LastName;
        user.Email = updateValues.Email;
        user.IsActive = updateValues.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        uow.Users.Update(user);
        await uow.SaveChangesAsync(ct);
        return user;
    }
}
