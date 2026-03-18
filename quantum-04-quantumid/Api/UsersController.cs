using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UsersApi.Application;
using UsersApi.Domain;

namespace UsersApi.Api;

/// <summary>
/// User management endpoints.
///
/// Login and token refresh are no longer here — QuantumID handles the full
/// OIDC flow. The frontend redirects users to https://id.quantumapi.eu,
/// gets back an access token, and sends it in every request as a Bearer token.
/// </summary>
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenIntrospectionService _introspection;

    public UsersController(
        IUserRepository userRepository,
        ITokenIntrospectionService introspection)
    {
        _userRepository = userRepository;
        _introspection = introspection;
    }

    /// <summary>
    /// Provision a user record after QuantumID has authenticated the user
    /// for the first time (post-registration hook or first login).
    /// The sub claim in the token becomes the stable user ID.
    /// </summary>
    [HttpPost("api/v1/users")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

        var created = await _userRepository.CreateAsync(user, cancellationToken);

        return CreatedAtAction(nameof(GetUser), new { id = created.Id }, MapToDto(created));
    }

    /// <summary>
    /// Returns the profile of the requesting user.
    /// A user can only read their own profile — the sub claim must match the
    /// requested id.
    /// </summary>
    [HttpGet("api/v1/users/{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var callerId = GetCallerId();
        if (callerId is null)
            return Unauthorized();

        // Ownership check: only the owner can read their own record
        if (callerId != id)
            return Forbid();

        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
            return NotFound();

        return Ok(MapToDto(user));
    }

    /// <summary>
    /// Soft-deletes the user record.
    /// Also calls QuantumID token introspection to verify the token has not
    /// been revoked — important before irreversible operations.
    /// </summary>
    [HttpDelete("api/v1/users/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var callerId = GetCallerId();
        if (callerId is null)
            return Unauthorized();

        if (callerId != id)
            return Forbid();

        // For destructive operations, confirm the token is still active in QuantumID.
        // JWT validation alone cannot detect revocation before expiry.
        var rawToken = GetRawBearerToken();
        if (rawToken is null || !await _introspection.IsActiveAsync(rawToken, cancellationToken))
            return Unauthorized(new { message = "Token is no longer active." });

        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user is null)
            return NotFound();

        await _userRepository.SoftDeleteAsync(id, cancellationToken);

        return NoContent();
    }

    // --- helpers ---

    private Guid? GetCallerId()
    {
        // QuantumID issues the subject as the "sub" claim.
        // ClaimTypes.NameIdentifier maps to the same claim in Microsoft's JWT middleware.
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");

        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private string? GetRawBearerToken()
    {
        var header = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (header is null || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        return header["Bearer ".Length..].Trim();
    }

    private static UserDto MapToDto(User user) =>
        new(user.Id, user.Email, user.FirstName, user.LastName, user.CreatedAt.UtcDateTime);
}

// DTOs — login/refresh DTOs removed; QuantumID owns those flows
public record CreateUserDto(string Email, string FirstName, string LastName);
public record UserDto(Guid Id, string Email, string FirstName, string LastName, DateTime CreatedAt);
