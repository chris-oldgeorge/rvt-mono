using Rvt.Reporting.Storage;
using Rvt.Storage;

namespace ReportingMonitor.Api.Storage;

public sealed class ConfiguredReportObjectUriResolver(Func<StorageObjectKey, Uri> resolveUri) : IReportObjectUriResolver
{
    private readonly Func<StorageObjectKey, Uri> _resolveUri = resolveUri
            ?? throw new ArgumentNullException(nameof(resolveUri));

    public Uri Resolve(StorageObjectKey key) => resolveUri(key);
}
