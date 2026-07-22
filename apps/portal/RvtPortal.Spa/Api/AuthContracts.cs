// File summary: Exposes API endpoints used by the React portal for auth contracts workflows.
// Major updates:
// - 2026-07-09 pending Refined generated DTO comments after controller workflow cleanup.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-03 f5fd01e Preserved React SPA/API host compatibility during provider update where applicable.

using System.ComponentModel.DataAnnotations;
namespace RvtPortal.Spa.Api;

public class AuthStateResponse
{
    public bool IsAuthenticated { get; set; }
    public AuthUserResponse? User { get; set; }

    // Function summary: Creates an unauthenticated auth-state response.
    public static AuthStateResponse Anonymous() => new();
}
public class AuthUserResponse
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Name { get; set; }
    public string? PhoneNumber { get; set; }
    public Guid? CompanyId { get; set; }
    public string? CompanyRole { get; set; }
    public List<string> Roles { get; set; } = [];
}
public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";
    [Required]
    public string Password { get; set; } = "";
    public required bool RememberMe { get; set; } = true;
}
public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";
}
public class ResetPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";
    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = "";
    [Required]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = "";
    [Required]
    public string Code { get; set; } = "";
}
public class ConfirmEmailResponse
{
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
}
public class SetInitialPasswordRequest
{
    [Required]
    public string UserId { get; set; } = "";
    [Required]
    public string Code { get; set; } = "";
    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string NewPassword { get; set; } = "";
    [Required]
    [Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = "";
}
public class ChangePasswordRequest
{
    [Required]
    public string OldPassword { get; set; } = "";
    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string NewPassword { get; set; } = "";
    [Required]
    [Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = "";
}
public class ProfileResponse
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Name { get; set; }
    public string? MobilePhone { get; set; }
    public string? Role { get; set; }
    public string? CompanyRole { get; set; }
    public string? CompanyName { get; set; }
}
public class UpdateProfileRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";
    public string? Name { get; set; }
    public string? MobilePhone { get; set; }
    public string? CompanyRole { get; set; }
}
public class MessageResponse
{
    public string Message { get; set; } = "";
}
