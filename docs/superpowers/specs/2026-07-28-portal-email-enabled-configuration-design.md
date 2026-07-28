# Portal Email Enabled Configuration Design

**Date:** 2026-07-28

## Goal

Allow the Portal host to enable or disable its SendGrid email provider through
the standard `RVT:EMAIL_ENABLED` configuration key. In environment variables,
the key is written as `RVT__Email_ENABLED` (configuration keys are
case-insensitive).

## Design

`ServiceCollectionExtensions` will read `RVT:EMAIL_ENABLED` using the existing
configuration instance when it registers `SendGridMailOptions`. The value will
default to `true`, preserving the current production behavior when the setting
is absent. The resolved value will become the registration's `Enabled` value.

The existing `EmailConfiguration` section continues to supply the SendGrid API
key and sender address. Its `UseDebugEmail` setting continues to redirect
recipients and is not repurposed as a disable switch. `Auth:SkipPasswordResetEmail`
continues to govern only the Portal account-workflow messages that already use
it.

## Testing

Add focused host-registration coverage that resolves the registered
`SendGridMailOptions` and proves:

1. With no `RVT:EMAIL_ENABLED` value, `Enabled` is `true`.
2. With `RVT:EMAIL_ENABLED=false`, `Enabled` is `false`.

The disabled case is the regression contract for a Visual Studio launch-profile
environment variable named `RVT__Email_ENABLED`.

## Scope

Only Portal SendGrid registration and its focused configuration test change.
No credentials, deployment secrets, `EmailConfiguration` values, or email
workflow behavior changes.
