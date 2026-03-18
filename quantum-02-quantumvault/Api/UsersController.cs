using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UsersApi.Application;
using UsersApi.Domain;

namespace UsersApi.Api;

[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly JwtService _jwtService;

    public UsersController(IUserRepository userRepository, JwtService jwtService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
    }

    [HttpPost("api/v1/users")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserDto dto,
        CancellationToken cancellationToken)
    {
        if (await _userRepository.ExistsAsync(dto.Email, cancellationToken))
            return Conflict(new { message = "A user with this email already exists." });

        var user = new User
        {
            Email = dto.Email.ToLowerInvariant(),
            FirstName = dto.FirstName,
            LastName = dto.LastName
        };

        var created = await _userRepository.CreateAsync(user, dto.Password, cancellationToken);

        var result = MapToDto(created);

        return CreatedAtAction(nameof(GetUser), new { id = created.Id }, result);
    }

    [HttpGet("api/v1/users/{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var callerId = GetCallerId();
        if (callerId == null)
            return Unauthorized();

        if (callerId != id)
            return Forbid();

        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
            return NotFound();

        return Ok(MapToDto(user));
    }

    [HttpDelete("api/v1/users/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var callerId = GetCallerId();
        if (callerId == null)
            return Unauthorized();

        if (callerId != id)
            return Forbid();

        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
            return NotFound();

        await _userRepository.SoftDeleteAsync(id, cancellationToken);

        return NoContent();
    }

    [HttpPost("api/v1/auth/login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login(
        [FromBody] LoginDto dto,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(dto.Email, cancellationToken);
        if (user == null)
            return Unauthorized(new { message = "Invalid email or password." });

        if (!_userRepository.VerifyPassword(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        var token = _jwtService.GenerateToken(user.Id);

        return Ok(new LoginResponseDto(token, user.Id));
    }

    private Guid? GetCallerId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");

        if (Guid.TryParse(sub, out var id))
            return id;

        return null;
    }

    private static UserDto MapToDto(User user) =>
        new(user.Id, user.Email, user.FirstName, user.LastName, user.CreatedAt);
}

public record LoginDto(string Email, string Password);
public record LoginResponseDto(string Token, Guid UserId);
