# RVT Provider Adapter Project Split Design

Date: 2026-07-23

Status: Approved architecture; implementation planning pending specification review.

## Objective

Isolate vendor SDKs, credentials, configuration validation, and provider-specific
implementation code in independently consumable projects. A consumer must take a
dependency on a provider deliberately; referencing RVT's provider-neutral contracts
must not pull SendGrid, Azure Identity, Azure Storage, AWS S3, or any other vendor SDK
into its dependency graph.

This is a clean, major-version split. It does not retain
`Rvt.Monitor.Common.Infrastructure` as a compatibility facade or meta-package.
All active consumers migrate in the same change set before the old project and package
are removed.

## Current State and Problem

`Rvt.Monitor.Common.Infrastructure` currently contains SendGrid email, Microsoft Graph
email, and TransmitSMS implementations. Its central `AddMonitorCommunications()`
registration method registers every implementation and selects the configured email
provider at runtime. The project therefore carries SendGrid and Azure Identity even for
consumers that do not use those providers.

`Rvt.Monitor.Common` currently contains the Local, Azure Blob, and S3 storage
implementations, their provider-union options object, and their DI selector. Its project
therefore carries Azure Identity, Azure Storage, and AWS S3 alongside provider-neutral
monitor behavior.

The release system assumes exactly three synchronized packages:
`Rvt.Monitor.Common`, `Rvt.Monitor.Common.Infrastructure`, and
`Rvt.Monitor.IntegrationTesting`. Package validation, package-only consumers, SBOM
generation, release-asset counts, solution membership, central locks, and release
scripts all encode that assumption and must change atomically with the split.

## Architectural Decisions

### Provider-neutral projects

`Rvt.Communication.Abstractions` owns only transport-neutral contracts:

- email and SMS delivery ports;
- email, SMS, and attachment request models;
- provider-neutral failure classifications and typed delivery exceptions;
- notification delivery and message-service contracts needed by monitor workflows.

It has no vendor SDK dependency and no provider selection logic.

`Rvt.Communication` implements the provider-neutral notification composer, delivery
workflow, and message-service behavior. It depends only on
`Rvt.Communication.Abstractions` plus the minimum Microsoft extension abstractions
needed by those workflows. Monitor alert, rule, and delivery code consumes the
abstractions, not concrete providers.

`Rvt.Storage.Abstractions` owns safe object keys, streaming request/response types,
provider-neutral storage failures, and the object-storage client contracts. It has no
cloud SDK, filesystem implementation, provider selector, or union of provider-specific
configuration fields.

### Communication provider projects

| Project | Owns | Dependencies |
| --- | --- | --- |
| `Rvt.Communication.SendGridMail` | SendGrid adapter, client factory, options, validation, and `AddSendGridMail()` | Abstractions, SendGrid, DI/config abstractions |
| `Rvt.Communication.MicrosoftGraphMail` | Graph adapter, Azure token provider, Graph models/JSON context, upload sessions, options, validation, and `AddMicrosoftGraphMail()` | Abstractions, Azure Identity, HTTP/DI/config abstractions |
| `Rvt.Communication.TransmitSms` | TransmitSMS client, adapter, models, options, validation, and `AddTransmitSms()` | Abstractions, HTTP/DI/config abstractions |

Each registration method registers exactly one provider and its port. Provider options
contain only that provider's settings and validate only when that provider is enabled.
Validation errors must name configuration keys without reflecting secret values or
provider response bodies.

There is no all-provider `Rvt.Communication.Infrastructure` or selector package. Host
composition chooses the provider. A SendGrid-only host references only the SendGrid
project. A monitor host that intentionally preserves runtime SendGrid/Graph choice may
reference both provider projects and perform the selection in its composition root; in
that case both dependencies are deliberate rather than transitive pollution from the
contracts package. TransmitSMS remains an independent optional channel rather than an
email-provider alternative.

### Storage provider projects

| Project | Owns | Dependencies |
| --- | --- | --- |
| `Rvt.Storage.Local` | Atomic local writes, protected root containment, streaming reads, and deletion | Storage abstractions only |
| `Rvt.Storage.AzureBlob` | Azure connection-string and managed-identity clients, container binding, streaming operations, and validation | Storage abstractions, Azure Identity, Azure Storage |
| `Rvt.Storage.S3` | AWS/compatible-S3 client configuration, bucket binding, streaming operations, and validation | Storage abstractions, AWS S3 SDK |

Each provider exposes an explicit registration method such as
`AddRvtLocalStorage()`, `AddRvtAzureBlobStorage()`, or `AddRvtS3Storage()`.
Provider selection remains in each application composition root. There is no provider
switch in `Rvt.Storage.Abstractions`.

## Object Storage Contract

The clean split replaces the current byte-array-only `IBlobStorageService` rather than
copying it into a new assembly. The new abstraction is centered on a named resource and
streaming content:

- `IObjectStorageClientFactory` binds an explicit logical resource to a provider client;
- `IObjectStorageClient` writes a stream, opens a read stream, and deletes an object if
  it exists;
- `StorageObjectKey` validates and normalizes provider-neutral object names;
- `StorageWriteRequest` carries a stream, content type, and object key;
- `StorageReadResult` carries the readable stream, content type, and length when known;
- `StorageWriteResult` returns the stable provider-neutral object key, not an
  authorization-bearing or provider-public URL.

A logical resource is mapped by host configuration to a local root, Azure container, or
S3 bucket/prefix. Provider projects own their provider-specific mapping options.
Abstractions never expose `BlobContainerClient`, `IAmazonS3`, Azure URIs, S3 URLs,
connection strings, or credentials.

## Composition and Configuration

Existing configuration key behavior is preserved during the communication migration,
including the current `RVT:` and `RVT__` forms, provider enablement defaults, and
SendGrid/Graph selection behavior. The key ownership moves into provider-specific
options types. Application composition roots remain responsible for interpreting the
host-level provider-selection setting and invoking the matching registration method.

Storage configuration is decomposed by provider. Shared resource names and prefixes
remain host configuration, while Azure connection/service URI and S3 bucket/region/
endpoint settings live only in the corresponding provider project.

No adapter assembly may log credentials, authorization headers, connection strings,
message destinations, or provider response bodies. Provider-specific exceptions are
translated into the shared failure model at the adapter boundary.

## Consumer Migration

The five monitor hosts that currently call `AddMonitorCommunications()` migrate to
explicit host composition. Hosts that retain runtime email-provider choice reference
both email packages intentionally; the Portal references only SendGrid until a product
decision enables another provider. Reporting and monitor-specific message senders
continue to depend on provider-neutral contracts.

Monitor and reporting consumers of the existing shared blob service migrate to the new
streaming storage abstraction and explicitly selected provider registration. Existing
object-name traversal protection, Local atomic-write behavior, configured prefixes, and
provider URI expectations are converted into provider-neutral object-key tests before
the old implementation is removed.

## Portal Blob Unification TODO

The provider-project split does not mechanically replace the Portal's current
`BlobStorageClientFactory`. The Portal needs capabilities that the old shared service
does not provide: streaming reads, multiple named containers, protected image delivery,
local fallback, archive/report reads, and stable persisted references.

Track this follow-up explicitly:

> TODO(storage): Migrate Portal `MonitorPictureStorage` and `SiteArchiveService` to
> `IObjectStorageClientFactory` after the provider split. Preserve protected API
> streaming, local fallback and atomic-write semantics, existing `blob://` monitor
> references, persisted archive URLs, and report/archive container boundaries. Do not
> expose provider URLs or authorization behavior through the generic storage port.

The follow-up migration order is monitor pictures, site archive/report reads, and then
an explicit product decision for customer logos. The reporting service's independent
Azure storage adapter is a separate candidate. The unused legacy
`RVT.Utilities.AzureBlobService` receives an independent deprecation decision rather
than being treated as a migration source.

## Package and Release Model

The major release publishes these packages:

1. `Rvt.Monitor.Common`
2. `Rvt.Monitor.IntegrationTesting`
3. `Rvt.Communication.Abstractions`
4. `Rvt.Communication`
5. `Rvt.Communication.SendGridMail`
6. `Rvt.Communication.MicrosoftGraphMail`
7. `Rvt.Communication.TransmitSms`
8. `Rvt.Storage.Abstractions`
9. `Rvt.Storage.Local`
10. `Rvt.Storage.AzureBlob`
11. `Rvt.Storage.S3`

`Rvt.Monitor.Common.Infrastructure` is not published in the new major version.
Package-to-package dependencies use the same exact release version. Central package
versions, locked restores, both solutions, source-boundary checks, release scripts,
package-version availability checks, SBOM inputs, package artifact assertions, and
release asset counts are updated to enumerate the new package graph.

Every provider receives a package-only smoke consumer proving that it restores and
resolves with only the expected transitive dependencies. The runtime consumer no longer
references one broad infrastructure package.

## Testing Strategy

### Architecture and dependency guards

- provider-neutral projects contain no vendor namespaces or SDK references;
- each vendor namespace and SDK package appears only in its provider project;
- storage SDKs are absent from `Rvt.Monitor.Common`;
- SendGrid and Azure Identity are absent from provider-neutral communication projects;
- active hosts do not reference the removed infrastructure project;
- duplicate email-port registration is rejected or prevented deterministically.

### Behavior tests

- existing SendGrid, Graph, and TransmitSMS adapter tests move with their providers;
- provider configuration tests preserve key aliases, enablement behavior, and
  secret-safe failures;
- notification composer and delivery workflow tests move to `Rvt.Communication`;
- Local, Azure, and S3 run the same storage contract suite;
- Local retains traversal, symlink, overwrite, and atomic-write tests;
- Azure and S3 tests verify named-resource binding, streaming, content type, prefixes,
  delete-if-exists behavior, and error translation;
- each host has a composition test proving the selected provider graph.

### Package and release tests

- all eleven packages build, pack, and contain symbols;
- exact inter-package versions are enforced;
- package-only consumers restore in locked and local-artifact modes;
- vulnerability checks include all new direct and transitive graphs;
- SBOM and release-manifest tests validate the complete package set;
- source consumers and the monorepo root solution build with no compatibility facade.

## Implementation Sequence

1. Add communication abstractions and provider-neutral orchestration projects with
   tests, without moving adapters.
2. Extract SendGrid, Graph, and TransmitSMS one at a time, moving each test suite and
   adding package dependency guards.
3. Migrate all active composition roots and direct consumers.
4. Remove `Rvt.Monitor.Common.Infrastructure` and update communication packaging,
   locks, release scripts, and consumers.
5. Add storage abstractions and contract tests.
6. Extract Local, Azure Blob, and S3 one at a time, moving their tests.
7. Migrate active monitor/reporting storage consumers and remove the old storage
   implementations and SDK references from `Rvt.Monitor.Common`.
8. Update the complete eleven-package release pipeline, SBOM, solutions, validation
   consumers, documentation, and major-version release notes.
9. Run the full monorepo, package-only, provider, audit, pack, and release-artifact
   verification gates.
10. Begin the separately reviewed Portal blob-unification follow-up.

Each provider extraction is a compile-green slice. A vendor implementation is never
duplicated across old and new assemblies; its source, tests, registrations, and package
dependency move atomically to prevent ambiguous type identity.

## Future Pending Work

The following work is explicitly recorded for later design and prioritization. None of
it is silently included in the provider-project extraction:

| Pending item | Trigger or constraint |
| --- | --- |
| Dynamic plugin discovery or runtime assembly loading | Consider only if deployments require providers to be installed without rebuilding a host. |
| Compatibility tooling for external consumers | The approved major release has no infrastructure facade. Revisit only if an external consumer cannot coordinate its migration. |
| Notification business-rule or message-content changes | Require a separate product specification and regression suite. |
| Public HTTP API or persisted monitor/report record changes | Require an explicit compatibility and data-migration design. |
| Portal blob-client/service unification | Begin after the provider split under the dedicated `IObjectStorageClientFactory` follow-up described above. |
| Legacy synchronous `IMessageService` removal | Migrate its remaining caller under a separate compatibility plan. |
| Database, MQTT, scheduling, and observability project boundaries | Evaluate as later dependency-isolation initiatives after communications and storage are complete. |

## Acceptance Criteria

- referencing either abstractions package introduces no vendor SDK dependency;
- a consumer can reference and test one provider independently;
- a SendGrid-only consumer does not receive Graph, TransmitSMS, Azure Storage, or S3;
- runtime-choice hosts include only the providers they explicitly reference;
- the Portal email path references only the SendGrid adapter package;
- `Rvt.Monitor.Common` contains no concrete storage adapter or cloud storage SDK;
- `Rvt.Monitor.Common.Infrastructure` is absent from solutions, active consumers,
  package outputs, release assets, and documentation describing the current graph;
- all eleven packages pass restore, build, tests, pack, vulnerability, SBOM, and
  package-consumer validation;
- the Portal blob-unification TODO is retained in project state and the implementation
  backlog until completed under its own approved design.
