namespace RvtPortal.Application.Ports.Vendors;

// Function summary: Carries the outcome of a vendor synchronization attempt without exposing transport details.
public sealed record VendorSyncResult(bool Succeeded, string? Error)
{
    // Function summary: Builds a successful vendor synchronization result.
    public static VendorSyncResult Success() => new(true, null);

    // Function summary: Builds a failed vendor synchronization result carrying the vendor-reported error.
    public static VendorSyncResult Failure(string error) => new(false, error);
}

public interface IVibrationVendorGateway
{
    // Function summary: Pushes the alert/caution level pair to the vibration vendor for the given monitor serial.
    Task<VendorSyncResult> UpdateAlertLevelsAsync(
        string serialId,
        double alertLevel,
        double cautionLevel,
        CancellationToken cancellationToken);
}
