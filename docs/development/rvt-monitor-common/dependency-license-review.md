# Dependency license review

Reviewed: 2026-07-16

## Scope and method

This review covers every distinct direct and transitive package/version pair resolved by the seven projects in `rvt-common.sln`. The inventory was generated with:

```bash
dotnet list rvt-common.sln package --include-transitive --format json --no-restore \
  > /private/tmp/rvt-reporting-dependency-inventory.json
```

The `--no-restore` form uses the checked-in lock files and existing restored assets. For each row below, the license and source values were read from the exact resolved package's cached `.nuspec`; no license was inferred from a package name. A package is marked direct if any solution project references that exact package/version directly, otherwise it is marked transitive.

The NuGet vulnerability audit was run against NuGet.org and the private RVT GitHub Packages feed:

```bash
dotnet list rvt-common.sln package --vulnerable --include-transitive --no-restore
```

It completed successfully and reported no vulnerable packages for all seven solution projects.

`Microsoft.Data.SqlClient.SNI.runtime` declares a packaged license file rather than an SPDX expression. Its resolved `LICENSE.txt` was inspected directly and contains Microsoft Software License Terms permitting object-code distribution as part of an application subject to the stated conditions. It is approved for this package use on that recorded basis. All other decisions below are based on the exact license expression or license URL recorded in the resolved `.nuspec`.

## Reviewed inventory

| Package ID | Resolved version | Relationship | License expression or URL | Repository/source URL | Decision |
| --- | --- | --- | --- | --- | --- |
| `AWSSDK.Core` | `4.0.100.3` | transitive | Expression: `Apache-2.0`; URL: <https://licenses.nuget.org/Apache-2.0> | Project/source: <https://github.com/aws/aws-sdk-net/> | Approved |
| `AWSSDK.S3` | `4.0.100.3` | direct | Expression: `Apache-2.0`; URL: <https://licenses.nuget.org/Apache-2.0> | Project/source: <https://github.com/aws/aws-sdk-net/> | Approved |
| `Azure.Core` | `1.47.1` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/Azure/azure-sdk-for-net> | Approved |
| `Azure.Identity` | `1.15.0` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/Azure/azure-sdk-for-net> | Approved |
| `Azure.Storage.Blobs` | `12.25.0` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/Azure/azure-sdk-for-net> | Approved |
| `Azure.Storage.Common` | `12.24.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/Azure/azure-sdk-for-net> | Approved |
| `Castle.Core` | `5.1.1` | transitive | Expression: `Apache-2.0`; URL: <https://licenses.nuget.org/Apache-2.0> | Repository: <https://github.com/castleproject/Core> | Approved |
| `MQTTnet` | `4.3.7.1207` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/MQTTnet.git> | Approved |
| `MQTTnet.Extensions.ManagedClient` | `4.3.7.1207` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/MQTTnet.git> | Approved |
| `MSTest.Analyzers` | `4.0.2` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/microsoft/testfx> | Approved |
| `MSTest.TestAdapter` | `4.0.2` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/microsoft/testfx> | Approved |
| `MSTest.TestFramework` | `4.0.2` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/microsoft/testfx> | Approved |
| `Microsoft.ApplicationInsights` | `2.23.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/Microsoft/ApplicationInsights-dotnet> | Approved |
| `Microsoft.Bcl.AsyncInterfaces` | `8.0.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/runtime> | Approved |
| `Microsoft.Bcl.Cryptography` | `9.0.13` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/runtime> | Approved |
| `Microsoft.Build.Tasks.Git` | `8.0.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/sourcelink> | Approved |
| `Microsoft.CodeCoverage` | `18.0.1` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/microsoft/vstest> | Approved |
| `Microsoft.Data.SqlClient` | `7.0.1` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/sqlclient> | Approved |
| `Microsoft.Data.SqlClient.Extensions.Abstractions` | `1.0.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/SqlClient> | Approved |
| `Microsoft.Data.SqlClient.Internal.Logging` | `1.0.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/SqlClient> | Approved |
| `Microsoft.Data.SqlClient.SNI.runtime` | `6.0.2` | transitive | Package file: `LICENSE.txt` (Microsoft Software License Terms); URL: <https://aka.ms/deprecateLicenseUrl> | Project/source: <https://aka.ms/sqlclientproject> | Approved |
| `Microsoft.EntityFrameworkCore` | `10.0.4` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.EntityFrameworkCore.Abstractions` | `10.0.4` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.EntityFrameworkCore.Analyzers` | `10.0.4` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.EntityFrameworkCore.InMemory` | `10.0.4` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.EntityFrameworkCore.Relational` | `10.0.4` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.EntityFrameworkCore.SqlServer` | `10.0.4` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.Caching.Abstractions` | `10.0.4` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.Caching.Memory` | `10.0.4` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.Configuration` | `10.0.9` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.Configuration.Abstractions` | `10.0.9` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.Configuration.Binder` | `10.0.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.DependencyInjection` | `10.0.4` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.4` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.Diagnostics.Abstractions` | `10.0.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.FileProviders.Abstractions` | `10.0.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.Hosting.Abstractions` | `10.0.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.Logging` | `10.0.4` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.Logging.Abstractions` | `10.0.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.Logging.Abstractions` | `10.0.4` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.Logging.Configuration` | `10.0.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.Options` | `10.0.4` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | `10.0.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Extensions.Primitives` | `10.0.9` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/dotnet> | Approved |
| `Microsoft.Identity.Client` | `4.73.1` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/AzureAD/microsoft-authentication-library-for-dotnet> | Approved |
| `Microsoft.Identity.Client.Extensions.Msal` | `4.73.1` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/AzureAD/microsoft-authentication-library-for-dotnet> | Approved |
| `Microsoft.IdentityModel.Abstractions` | `8.16.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet> | Approved |
| `Microsoft.IdentityModel.JsonWebTokens` | `8.16.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet> | Approved |
| `Microsoft.IdentityModel.Logging` | `8.16.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet> | Approved |
| `Microsoft.IdentityModel.Protocols` | `8.16.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet> | Approved |
| `Microsoft.IdentityModel.Protocols.OpenIdConnect` | `8.16.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet> | Approved |
| `Microsoft.IdentityModel.Tokens` | `8.16.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet> | Approved |
| `Microsoft.NET.Test.Sdk` | `18.0.1` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/microsoft/vstest> | Approved |
| `Microsoft.SourceLink.Common` | `8.0.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/sourcelink> | Approved |
| `Microsoft.SourceLink.GitHub` | `8.0.0` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/sourcelink> | Approved |
| `Microsoft.SqlServer.Server` | `1.0.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/sqlclient> | Approved |
| `Microsoft.SqlServer.TransactSql.ScriptDom` | `180.37.3` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/microsoft/SqlScriptDOM> | Approved |
| `Microsoft.TestPlatform.AdapterUtilities` | `18.0.1` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/microsoft/vstest> | Approved |
| `Microsoft.TestPlatform.ObjectModel` | `18.0.1` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/microsoft/vstest> | Approved |
| `Microsoft.TestPlatform.TestHost` | `18.0.1` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/microsoft/vstest> | Approved |
| `Microsoft.Testing.Extensions.Telemetry` | `2.0.2` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/microsoft/testfx> | Approved |
| `Microsoft.Testing.Extensions.TrxReport.Abstractions` | `2.0.2` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/microsoft/testfx> | Approved |
| `Microsoft.Testing.Extensions.VSTestBridge` | `2.0.2` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/microsoft/testfx> | Approved |
| `Microsoft.Testing.Platform` | `2.0.2` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/microsoft/testfx> | Approved |
| `Microsoft.Testing.Platform.MSBuild` | `2.0.2` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/microsoft/testfx> | Approved |
| `Moq` | `4.20.72` | direct | Expression: `BSD-3-Clause`; URL: <https://licenses.nuget.org/BSD-3-Clause> | Repository: <https://github.com/moq/moq> | Approved |
| `Newtonsoft.Json` | `13.0.1` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/JamesNK/Newtonsoft.Json> | Approved |
| `Newtonsoft.Json` | `13.0.3` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/JamesNK/Newtonsoft.Json> | Approved |
| `Npgsql` | `10.0.3` | direct | Expression: `PostgreSQL`; URL: <https://licenses.nuget.org/PostgreSQL> | Repository: <https://github.com/npgsql/npgsql> | Approved |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `10.0.2` | direct | Expression: `PostgreSQL`; URL: <https://licenses.nuget.org/PostgreSQL> | Repository: <https://github.com/npgsql/efcore.pg> | Approved |
| `OpenTelemetry` | `1.16.0` | transitive | Expression: `Apache-2.0`; URL: <https://licenses.nuget.org/Apache-2.0> | Repository: <https://github.com/open-telemetry/opentelemetry-dotnet> | Approved |
| `OpenTelemetry.Api` | `1.16.0` | transitive | Expression: `Apache-2.0`; URL: <https://licenses.nuget.org/Apache-2.0> | Repository: <https://github.com/open-telemetry/opentelemetry-dotnet> | Approved |
| `OpenTelemetry.Api.ProviderBuilderExtensions` | `1.16.0` | transitive | Expression: `Apache-2.0`; URL: <https://licenses.nuget.org/Apache-2.0> | Repository: <https://github.com/open-telemetry/opentelemetry-dotnet> | Approved |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | `1.16.0` | direct | Expression: `Apache-2.0`; URL: <https://licenses.nuget.org/Apache-2.0> | Repository: <https://github.com/open-telemetry/opentelemetry-dotnet> | Approved |
| `OpenTelemetry.Extensions.Hosting` | `1.16.0` | direct | Expression: `Apache-2.0`; URL: <https://licenses.nuget.org/Apache-2.0> | Repository: <https://github.com/open-telemetry/opentelemetry-dotnet> | Approved |
| `OpenTelemetry.Instrumentation.AspNetCore` | `1.16.0` | direct | Expression: `Apache-2.0`; URL: <https://licenses.nuget.org/Apache-2.0> | Repository: <https://github.com/open-telemetry/opentelemetry-dotnet-contrib> | Approved |
| `OpenTelemetry.Instrumentation.Http` | `1.16.0` | direct | Expression: `Apache-2.0`; URL: <https://licenses.nuget.org/Apache-2.0> | Repository: <https://github.com/open-telemetry/opentelemetry-dotnet-contrib> | Approved |
| `OpenTelemetry.Instrumentation.Runtime` | `1.15.1` | direct | Expression: `Apache-2.0`; URL: <https://licenses.nuget.org/Apache-2.0> | Repository: <https://github.com/open-telemetry/opentelemetry-dotnet-contrib> | Approved |
| `Quartz` | `3.18.1` | transitive | Expression: `Apache-2.0`; URL: <https://licenses.nuget.org/Apache-2.0> | Repository: <https://github.com/quartznet/quartznet> | Approved |
| `Quartz.Extensions.DependencyInjection` | `3.18.1` | transitive | Expression: `Apache-2.0`; URL: <https://licenses.nuget.org/Apache-2.0> | Repository: <https://github.com/quartznet/quartznet> | Approved |
| `Quartz.Extensions.Hosting` | `3.18.1` | direct | Expression: `Apache-2.0`; URL: <https://licenses.nuget.org/Apache-2.0> | Repository: <https://github.com/quartznet/quartznet> | Approved |
| `SendGrid` | `9.29.3` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/sendgrid/sendgrid-csharp.git> | Approved |
| `System.ClientModel` | `1.5.1` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/Azure/azure-sdk-for-net> | Approved |
| `System.Configuration.ConfigurationManager` | `9.0.13` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/runtime> | Approved |
| `System.Diagnostics.EventLog` | `9.0.13` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/runtime> | Approved |
| `System.IO.Hashing` | `8.0.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/runtime> | Approved |
| `System.IdentityModel.Tokens.Jwt` | `8.16.0` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet> | Approved |
| `System.Memory.Data` | `8.0.1` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/runtime> | Approved |
| `System.Security.Cryptography.Pkcs` | `9.0.13` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/runtime> | Approved |
| `System.Security.Cryptography.ProtectedData` | `9.0.13` | transitive | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/dotnet/runtime> | Approved |
| `coverlet.collector` | `6.0.2` | direct | Expression: `MIT`; URL: <https://licenses.nuget.org/MIT> | Repository: <https://github.com/coverlet-coverage/coverlet.git> | Approved |
| `starkbank-ecdsa` | `1.3.3` | transitive | URL: <https://opensource.org/licenses/MIT> | Project/source: <https://github.com/starkbank/ecdsa-dotnet> | Approved |
