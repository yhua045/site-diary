namespace SiteDiary.Application.DTOs;

public record UserDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    bool IsActive,
    bool IsArchived,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email);

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    bool IsActive);
