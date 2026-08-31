using Microsoft.AspNetCore.Identity;

namespace PPECB.Infrastructure.Identity;

/// <summary>
/// The application's user. It lives in Infrastructure rather than Domain so the Domain
/// layer stays free of any ASP.NET Identity dependency; domain entities refer to the
/// owner by id only.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public DateTime CreatedDate { get; set; }
}
