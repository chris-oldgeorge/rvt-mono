# Development secrets reference

This document explains every configuration key that
[`set-dev-secrets.ps1`](set-dev-secrets.ps1) writes, what each one does, and
which are sensitive. It is a reference, not a store of values — **no real
credentials belong in this file or in any tracked file.**

## How secrets are stored

- **Development:** the script writes to the .NET user-secrets store for
  `RvtPortal.Spa`, so nothing lands in `appsettings*.json` or the repository.
  `appsettings.json` only carries blank placeholders documenting the shape.
- **Production:** the sensitive values come from the deployment secret store
  (environment variables / Azure Key Vault), never from the script.

Run the core set:

```powershell
.\docs\deploy\set-dev-secrets.ps1
```

Add optional integrations with the `-Configure*` switches, or all at once:

```powershell
.\docs\deploy\set-dev-secrets.ps1 -ConfigureAll
```

List the configured keys without exposing values:

```powershell
dotnet user-secrets list --project RvtPortal.Spa\RvtPortal.Spa.csproj |
  ForEach-Object { ($_ -split ' = ', 2)[0] }
```

## Core keys (always set)

| Key | Purpose | Sensitive |
| --- | --- | --- |
| `Database:Provider` | Selects the EF provider: `SqlServer` or `Postgres`. | No |
| `Database:ConnectionStringName` | Name of the connection-strings entry to use (`DefaultConnection`). | No |
| `Database:ConnectionString` | The database connection string the data layer reads. | **Yes** |
| `Database:PostgresRoutineSchema` | Schema that holds PostgreSQL stored routines (`public`). | No |
| `ConnectionStrings:DefaultConnection` | Same connection string under the standard EF key; set to the same value. | **Yes** |
| `Auth:SkipPasswordResetEmail` | When `true`, password-reset/account emails are skipped instead of sent — the safe local default. Flipped to `false` automatically when outbound email is configured. | No |
| `RVT_PORTAL_SEED_MASTER_ADMIN` | Password for the seeded master-admin account. Must satisfy Identity rules (≥6 chars, upper, lower, digit, symbol). | **Yes** |
| `EmailConfiguration:UseDebugEmail` | When `true`, every outgoing email is redirected to `DebugEmailAddress` instead of the real recipient. | No |
| `EmailConfiguration:DebugEmailAddress` | The sink address that receives all mail while `UseDebugEmail` is on. | No |
| `EmailConfiguration:CopyEmailAddress` | Address copied on outgoing mail. | No |
| `EmailConfiguration:Sending_Email_Address` | The `From` address on portal email. | No |
| `BlobStorage:MonitorImagesContainer` | Container name for monitor images (`monitor-pictures`). | No |
| `BlobStorage:ArchiveContainer` | Container name for site archives (`archive`). | No |
| `BlobStorage:ReportContainer` | Container name for generated PDF reports (`pdfreports`). | No |
| `BlobStorage:ReportFolder` | Folder/prefix within the report container (`rvtreports`). | No |
| `BlobStorage:AudioFolder` | Folder for audio files (`audiofiles`). | No |

## Optional integrations

### Report generation — `-ConfigureReporting` *(new)*

The portal offloads PDF report building to an external report-generation
microservice, and that service calls back into the portal for report content
(for example a site's customer logo). The two `InternalApiKey` values secure the
two directions of that trust and must match the values held by the report
service.

| Key | Purpose | Sensitive |
| --- | --- | --- |
| `ReportGenerationService:BaseUrl` | Base URL of the report-generation service the portal calls. Reporting returns *service unavailable* if it is blank. | No |
| `ReportGenerationService:InternalApiKey` | Sent by the portal as an internal-key header to authenticate itself **to** the report service (portal → service). | **Yes** |
| `ReportContent:InternalApiKey` | Shared key the report service presents when calling **back** into the portal; the portal compares it in constant time before returning content (service → portal). | **Yes** |

### Omnidots vibration adapter — `-ConfigureOmnidots`

| Key | Purpose | Sensitive |
| --- | --- | --- |
| `ExternalUrls:OmnidotsAdapterUrl` | URL of the Omnidots vibration-sync adapter. | No |
| `ExternalUrls:OmnidotsAdapterSecret` | Secret used to authenticate to that adapter. | **Yes** |

### What3Words — `-ConfigureWhat3Words`

| Key | Purpose | Sensitive |
| --- | --- | --- |
| `What3Words:ApiKey` | API key for what3words address conversion. | **Yes** |

### Azure Blob Storage — `-ConfigureBlobStorage`

Choose one mode with `-BlobStorageMode` (default `ConnectionString`).

| Key | Purpose | Sensitive |
| --- | --- | --- |
| `BlobStorage:blobConnectionString` | Azure Storage connection string (`ConnectionString` mode). | **Yes** |
| `BlobStorage:blobServiceUri` | Azure Blob service URI, used with managed identity (`ServiceUri` mode). | No |

### Outbound email (SendGrid) — `-ConfigureOutboundEmail`

Enabling this also sets `Auth:SkipPasswordResetEmail` to `false` so real mail is
sent.

| Key | Purpose | Sensitive |
| --- | --- | --- |
| `EmailConfiguration:SENDGRID_API_KEY` | SendGrid API key used to send real outbound email. | **Yes** |

### SMTP — `-ConfigureSmtp`

| Key | Purpose | Sensitive |
| --- | --- | --- |
| `SmtpServer:Host` | SMTP server host name. | No |
| `SmtpServer:Port` | SMTP port (default `587`). | No |
| `SmtpServer:Ssl` | Whether to use SSL/TLS (default `true`). | No |
| `SmtpServer:Username` | SMTP account user name. | **Yes** |
| `SmtpServer:Password` | SMTP account password. | **Yes** |

### Redis distributed cache — `-ConfigureRedis`

| Key | Purpose | Sensitive |
| --- | --- | --- |
| `RvtProduction:RedisConnectionString` | Redis connection string for the distributed cache. | **Yes** |
| `RvtProduction:RedisInstanceName` | Key prefix for this app's cache entries (`RvtPortal:`). | No |

### Data protection — `-ConfigureDataProtection`

Required outside Development so ASP.NET Core data-protection keys persist across
restarts and instances (otherwise cookies/tokens break on redeploy).

| Key | Purpose | Sensitive |
| --- | --- | --- |
| `RvtProduction:DataProtectionApplicationName` | Application name that isolates this app's data-protection keys (`RvtMonitoring`). | No |
| `RvtProduction:DataProtectionBlobUri` | Azure Blob URI where the data-protection key ring is stored. | No |
| `RvtProduction:DataProtectionKeyIdentifier` | Azure Key Vault key identifier used to encrypt (wrap) the key ring at rest. | No |

## Related non-secret configuration

These are read from configuration but are environment/topology settings, not
secrets, and the script does not set them:

- `Spa:AllowedOrigins` — CORS origins allowed to call the API.
- `Spa:PublicBaseUrl` — the portal's public base URL, used to build absolute
  links (e.g. password-reset and account-activation links) in outgoing email.
- `TimeZones:Local` — the configured local time zone (e.g. `GMT Standard Time`);
  the domain stores UTC and converts for display.
- `DefaultMonitorLocation:Lat` / `:Lng` — fallback map coordinates.
