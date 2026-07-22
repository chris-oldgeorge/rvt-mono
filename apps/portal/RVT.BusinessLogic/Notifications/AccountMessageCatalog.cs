// File summary: Holds the account-lifecycle email templates (subject + HTML body) sent by the portal.
// Major updates:
// - 2026-07-15 pending Extracted the password-set/reset templates from the retired MessageService into a pure,
//   infrastructure-free catalog. The former monitor alert/caution/SMS templates were not carried over: the
//   portal never sent them (no caller), and monitor-alert notification is the monitor-worker domain's concern.

namespace RVT.BusinessLogic.Notifications;

public enum AccountMessageKind
{
    PasswordSet,
    PasswordReset
}

// Function summary: An account email's subject and HTML body, with a {callbackUrl} placeholder for the action link.
public sealed record AccountMessage(string Subject, string HtmlBody);

public static class AccountMessageCatalog
{
    // Preserved verbatim from the retired MessageService so rendered emails stay byte-identical.
    private const string Template =
        "<!DOCTYPE html>\r\n<html lang=\"en\">" +
        "<head>" +
        "<meta charset=\"UTF-8\">" +
        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">" +
        "<meta http-equiv=\"X-UA-Compatible\" content=\"ie=edge\">" +
        "<title>RVT Cloud</title>" +
        "</head> " +
        "<body>{Content}" +
        "</body>" +
        "</html>";

    private const string PasswordSetContent =
        "You have been added as a user to the RVT Cloud. <br/><br/>" +
        "Please set a password for your account by <a href='{callbackUrl}'>clicking here</a>. <br/><br/>" +
        "Sign into your account to access your measurements,  alerts and reports.  <br/> <br/>" +
        "Many thanks From the RVT Group";

    private const string PasswordResetContent =
        "You have requested to reset your password to the RVT Cloud.<br/><br/>" +
        "Please <a href='{callbackUrl}'>click here</a> to reset the password. <br/><br/>" +
        "Many thanks From the RVT Group";

    // Function summary: Returns the subject and HTML body for an account message kind.
    public static AccountMessage For(AccountMessageKind kind) => kind switch
    {
        AccountMessageKind.PasswordSet => new AccountMessage(
            "Welcome to the RVT Cloud",
            Template.Replace("{Content}", PasswordSetContent, StringComparison.Ordinal)),
        AccountMessageKind.PasswordReset => new AccountMessage(
            "Password reset",
            Template.Replace("{Content}", PasswordResetContent, StringComparison.Ordinal)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown account message kind.")
    };
}
