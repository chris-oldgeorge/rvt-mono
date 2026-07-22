// File summary: Maps installer application-service models to existing frontend-facing API contracts.
// Major updates:
// - 2026-07-09 pending Added installer read-service mappers for controller cleanup.

using RvtPortal.Spa.Application.Installers;

namespace RvtPortal.Spa.Api.Mappers;

public static class InstallerApiMapper
{
    // Function summary: Maps installer monitor status models to the existing API response contract.
    public static InstallerMonitorStatusResponse ToStatusResponse(InstallerMonitorStatusModel model)
    {
        return new InstallerMonitorStatusResponse
        {
            MonitorId = model.MonitorId,
            LastDataTime = model.LastDataTime,
            IsOffline = model.IsOffline,
            Status = model.Status
        };
    }
}
