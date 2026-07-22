# SPA Authentication And Authorization
The SPA keeps the MVC portal's ASP.NET Identity cookie model. It does not store bearer tokens in browser storage.
## Authentication Stream
1. The React app calls `GET /api/auth/me` on startup.
2. Anonymous users see the SPA login, forgot-password, reset-password, or confirm-email flow.
3. `POST /api/auth/login` resolves the submitted email with `UserManager.FindByEmailAsync(...)`, rejects disabled accounts, then calls the user-based `SignInManager.PasswordSignInAsync(...)` overload. Login is email-only; legacy username fallback is intentionally not supported.
4. Successful login returns the current auth state and the server sets the normal Identity application cookie.
5. `POST /api/auth/logout` clears the Identity cookie and returns an anonymous auth state.
6. Profile and password changes refresh the Identity sign-in so the cookie claims remain current.
## Password And Email Flows
- `POST /api/auth/forgot-password` sends the same password reset email content used by the MVC portal.
- Reset links target `/reset-password?code=...`, which is handled by React and submitted to `POST /api/auth/reset-password`.
- Confirmation links target `/confirm-email?userId=...&code=...`. React confirms the email through `GET /api/auth/confirm-email`, then sets the initial password through `POST /api/auth/confirm-email`.
- The SPA requires the original confirmation code again when setting the initial password. This preserves the MVC two-step user experience while avoiding a bare `userId` password-set request.
## Authorization Methodology
- API endpoints use ASP.NET Core `[Authorize]` attributes and role checks on the server.
- `RoleAuthorization.AdminRoles` centralizes the RVT admin role list: `RVTMasterAdmin,RVTAdmin`.
- `/api/companies` requires admin roles, matching the MVC company controller.
- `/api/lookups` requires an authenticated Identity cookie.
- Cookie redirects for `/api/*` are converted to `401` or `403` responses so the SPA can update its auth stream instead of receiving HTML redirects.

## Roles

| Role | Intended access |
|---|---|
| `RVTMasterAdmin` | Full portal administration. Can use master/admin functionality and should be accepted anywhere `RoleAuthorization.AdminRoles` is required. |
| `RVTAdmin` | Standard RVT administration. Can use admin-scoped company, lookup, user, contract, site, monitor, notification, reporting, dashboard, map, calendar, and data-view workflows. |
| `RVTInstaller` | Installer workflow access scoped to the installer's assigned company. Limited to installer monitor, deployment, and status routes for current deployments under that company. |
| `CompanyUser` | Customer/company scoped access. Sees data for assigned company, sites, contracts, monitors, notifications, dashboards, maps, calendar entries, and data views. |
| Anonymous | Public auth-only flows: login, forgot password, reset password, confirm email. Business APIs must return `401`. |

## Navigation Matrix

The matrix is derived from the MVC `_TopNavBar.cshtml` and `_LoginPartial.cshtml`.

| Role | Home | Sites | Monitors | Companies | Account | Notes |
|---|---:|---:|---:|---:|---:|---|
| `RVTMasterAdmin` | Yes | Yes | Yes | Yes | Yes | Full admin navigation. |
| `RVTAdmin` | Yes | Yes | Yes | Yes | Yes | Same core shell permissions as master admin, with master-only server checks where required. |
| `CompanyUser` | Yes | Yes | Yes | No | Yes | Company/site-scoped business navigation. |
| `RVTInstaller` | Yes | No | Yes | No | Yes | Installer monitor workflow only for the assigned company. |
| Anonymous | No | No | No | No | No | Public auth routes only. |

## Seed/Test Credentials

The SPA seed can create or reset the development master admin outside the `Testing` environment only when a seed credential is supplied through configuration:

| User | Credential Source | Role | Purpose |
|---|---|---|---|
| `master@rvtGroup.com` | `RVT_PORTAL_SEED_MASTER_ADMIN` | `RVTMasterAdmin` | Optional baseline smoke user for admin login and protected API checks in development data. The account is skipped when the setting is not configured and the user does not already exist. |

Executable tests seed these deterministic users in isolated in-memory stores:

| User | Password | Role | Verification |
|---|---|---|---|
| `admin@rvt.test` | `P8sSw0rd9$` | `RVTAdmin` | Can log in, receives auth cookie, sees admin shell, can request admin Companies route in the SPA shell. |
| `installer@rvt.test` | `P8sSw0rd9$` | `RVTInstaller` | Can log in, sees installer shell, receives `403` for `/api/companies`, cannot trigger the SPA Companies API from direct navigation, and is seeded with an assigned company for installer object-scope checks. |
| `disabled@rvt.test` | `P8sSw0rd9$` | `RVTAdmin` | Login returns `403` with disabled-account detail. |
| `new.user@rvt.test` | set during test | `CompanyUser` | Confirm-email and set-initial-password flow signs in after token verification. |

## Protected Endpoint Expectations

| Endpoint | Anonymous | `RVTInstaller` | `RVTAdmin` / `RVTMasterAdmin` |
|---|---:|---:|---:|
| `GET /api/auth/me` | `200` anonymous state | `200` authenticated state | `200` authenticated state |
| `POST /api/auth/logout` | `401` | `200` | `200` |
| `GET /api/auth/profile` | `401` | `200` | `200` |
| `PUT /api/auth/profile` | `401` | `200` | `200` |
| `POST /api/auth/password` | `401` | `200` | `200` |
| `GET /api/lookups/companies` | `401` | `200` once lookup role scoping is expanded | `200` |
| `GET /api/companies` | `401` | `403` | `200` |
| `GET /api/installer/monitors` | `401` | `200` scoped to assigned company | `200` |
| `GET /api/installer/monitors/{id}` | `401` | `200` for assigned-company monitors; `404` outside company | `200` |
| `GET /api/installer/monitors/{id}/status` | `401` | `200` for assigned-company monitors; `404` outside company | `200` |
| `PUT /api/installer/deployments/{id}` | `401` | `204` for assigned-company deployments; `404` outside company | `204` |

## Installer Object Scope

`RVTInstaller` users must carry a `CompanyId`. Installer monitor inventory and object endpoints only expose current deployments whose contract `CompanyId` matches the installer account. Cross-company monitor and deployment identifiers return `404` rather than leaking object existence.

## Authorization Gates

Current executable coverage:

| Check | Expected result | Current evidence |
|---|---|---|
| Anonymous `GET /api/health` | `200` | Covered by `RvtPortal.Spa.Tests/SpaHostSmokeTests.cs`. |
| Swagger/OpenAPI in `Testing` | `200` for `/swagger/v1/swagger.json` | Covered by `RvtPortal.Spa.Tests/SpaHostSmokeTests.cs` and `npm run openapi`. |
| Anonymous SPA shell | Login form renders | Covered by `src/App.test.tsx` and `tests/e2e/auth-shell.spec.ts`. |
| Valid login | `200`, auth state, Identity cookie | `Login_ReturnsAuthStateAndCookie_ForValidUser`. |
| Invalid login | `401` with MVC-compatible generic detail | `Login_ReturnsGenericUnauthorizedMessage_ForInvalidCredentials`. |
| Legacy username-only login | `401`; email address must match the registered email, not only `UserName` | `Login_ReturnsUnauthorized_ForLegacyUsernameOnlyMatch`. |
| Disabled login | `403` | `Login_ReturnsForbidden_ForDisabledUser`. |
| Logout | `200`, subsequent `me` is anonymous | `Logout_ClearsSignedInSession`. |
| Forgot password | Same public response for known/unknown email | `ForgotPassword_ReturnsSameMessage_ForKnownAndUnknownEmail`. |
| Reset password | Valid token changes password | `ResetPassword_ChangesPassword_WithValidToken`. |
| Confirm email | Valid token confirms email and set-password signs in | `ConfirmEmail_ConfirmsUserAndSetInitialPasswordSignsIn`. |
| Confirm email replay | Reused confirmation link returns `404` | `ConfirmEmail_ReturnsNotFound_WhenLinkIsReused`. |
| Initial password proof | Missing/malformed confirmation proof cannot set password | `ConfirmEmail_RequiresOriginalCodeToSetInitialPassword`. |
| Profile/password | Signed-in user can read/update profile and change password | `ProfileAndPasswordEndpoints_UpdateSignedInUser`. |
| Protected API status codes | Anonymous gets `401`; wrong role gets `403` | `ProtectedEndpoints_Return401ForAnonymous_And403ForWrongRole`. |
| Installer object scope | Installer list/detail/status/deployment updates are limited to the installer's assigned company | `MonitorInventoryStates_AreFilteredByStateAndRole` and `InstallerEndpoints_AreScopedToInstallerCompany`. |
| Role-aware SPA shell | Admin sees Companies; installer does not | `src/App.test.tsx` and `tests/e2e/auth-shell.spec.ts`. |

Verification commands:

```powershell
dotnet build .\RvtPortal.Spa.sln -v minimal
dotnet test .\RvtPortal.Spa.sln --no-build -v minimal
```

```powershell
Set-Location .\RvtPortal.Client
npm run lint
npm run build
npm run test:run
npm run test:e2e
npm run openapi
```
