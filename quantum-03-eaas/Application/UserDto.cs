namespace UsersApi.Application;

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    DateTimeOffset CreatedAt
);
