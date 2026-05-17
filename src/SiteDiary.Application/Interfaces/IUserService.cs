using SiteDiary.Domain.Entities;

namespace SiteDiary.Application.Interfaces;

public interface IUserService
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default);
    Task<User?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<User> CreateAsync(User user, CancellationToken ct = default);
    Task<User?> UpdateAsync(int id, User updateValues, CancellationToken ct = default);
}
