using Microsoft.AspNetCore.Identity;
using PPECB.Application.Abstractions;
using PPECB.Application.DTOs;
using PPECB.Domain.Exceptions;
using PPECB.Infrastructure.Identity;

namespace PPECB.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ITokenService _tokens;
    private readonly IDateTimeProvider _clock;

    public AuthService(
        UserManager<ApplicationUser> users,
        ITokenService tokens,
        IDateTimeProvider clock)
    {
        _users = users;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default)
    {
        var email = dto.Email.Trim();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            CreatedDate = _clock.UtcNow
        };

        var result = await _users.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            // Identity reports password-policy and duplicate-email failures here. Group
            // them per field so the client can show them next to the right input.
            var errors = result.Errors
                .GroupBy(e => e.Code.Contains("Password", StringComparison.OrdinalIgnoreCase)
                    ? nameof(RegisterRequestDto.Password)
                    : nameof(RegisterRequestDto.Email))
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

            throw new Domain.Exceptions.ValidationException(errors);
        }

        return BuildResponse(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(dto.Email.Trim());

        // Deliberately identical failure for "no such user" and "wrong password" so the
        // endpoint cannot be used to enumerate which email addresses are registered.
        if (user is null || !await _users.CheckPasswordAsync(user, dto.Password))
        {
            throw new BusinessRuleException("Invalid email or password.");
        }

        return BuildResponse(user);
    }

    private AuthResponseDto BuildResponse(ApplicationUser user)
    {
        var (token, expiresAt) = _tokens.CreateToken(user.Id, user.Email ?? string.Empty);

        return new AuthResponseDto
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            Token = token,
            ExpiresAtUtc = expiresAt
        };
    }
}
