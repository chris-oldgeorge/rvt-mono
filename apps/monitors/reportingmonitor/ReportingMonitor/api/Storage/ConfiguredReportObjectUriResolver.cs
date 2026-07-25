using Rvt.Reporting.Storage;
using Rvt.Storage;

namespace ReportingMonitor.Api.Storage;

public sealed class ConfiguredReportObjectUriResolver : IReportObjectUriResolver
{
    private readonly Func<StorageObjectKey, Uri> resolveUri;

    public ConfiguredReportObjectUriResolver(Func<StorageObjectKey, Uri> resolveUri)
    {
        this.resolveUri = resolveUri
            ?? throw new ArgumentNullException(nameof(resolveUri));
    }

    public Uri Resolve(StorageObjectKey key) => resolveUri(key);
}
