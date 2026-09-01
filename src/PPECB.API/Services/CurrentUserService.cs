using System.Security.Claims;
using PPECB.Application.Abstractions;

namespace PPECB.API.Services;

/// <summary>
/// Resolves the caller from the validated JWT on the current request. This is the single
/// source of identity for the whole application — audit stamping and the ownership query
/// filters both read from here, so nothing downstream can be told a different user id.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public string? UserId =>
        Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Principal?.FindFirstValue("sub");

    public string? Email =>
        Principal?.FindFirstValue(ClaimTypes.Email)
        ?? Principal?.FindFirstValue("email");

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string RequireUserId() =>
        UserId ?? throw new UnauthorizedAccessException("The request is not authenticated.");
}
