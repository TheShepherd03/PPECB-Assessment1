using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PPECB.Application.Abstractions;
using PPECB.Application.DTOs;

namespace PPECB.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Registers a new user and returns a token for immediate sign-in.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] RegisterRequestDto request, CancellationToken ct)
    {
        var response = await _auth.RegisterAsync(request, ct);
        return Ok(response);
    }

    /// <summary>Signs a user in with email and password.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginRequestDto request, CancellationToken ct)
    {
        var response = await _auth.LoginAsync(request, ct);
        return Ok(response);
    }
}
