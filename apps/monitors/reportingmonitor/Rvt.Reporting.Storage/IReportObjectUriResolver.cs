using Rvt.Storage;

namespace Rvt.Reporting.Storage;

public interface IReportObjectUriResolver
{
    Uri Resolve(StorageObjectKey key);
}
