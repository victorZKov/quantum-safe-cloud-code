using System.ComponentModel.DataAnnotations;

namespace UsersApi.Application;

public record CreateUserDto(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string FirstName,
    [Required] string LastName
);
