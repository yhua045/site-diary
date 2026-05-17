namespace SiteDiary.Application.DTOs;

public record UserDto(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    bool IsArchived);

public record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email);

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string Email);
