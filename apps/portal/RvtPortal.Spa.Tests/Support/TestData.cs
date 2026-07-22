// File summary: Central object-mother for RVT domain entities in tests, with defaults drawn from real dev data.
// Major updates:
// - 2026-07-15 pending Added to replace ~145 inline entity constructions and remove per-file default drift.

using RVT.Entities;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// Builds valid RVT domain entities for tests. Defaults are modelled on the shapes actually seen in the dev
/// <c>rvt</c> PostgreSQL database (UK construction companies, <c>POSTCODE - Location</c> site names, five-digit
/// serials, Palas/AQGuard vibration monitors, <c>///word.word.word</c> what3words, Peak/LAeq/pm10 alert fields),
/// so a monitor or site built here reads like a real one.
///
/// Discipline: this factory supplies <em>context</em>, never a value a test asserts on. If a test checks a
/// field, it passes that field explicitly at the call site - the default is only there so the entity is valid.
/// Every field a test commonly cares about is an optional parameter. IDs and serials are unique per call, so a
/// test can build several of the same entity without collisions.
/// </summary>
internal static class TestData
{
    private static int sequence;

    // Function summary: Returns a process-unique, monotonically increasing counter for unique names/serials/ids.
    private static int Next() => System.Threading.Interlocked.Increment(ref sequence);

    // Function summary: Rotates deterministically through a set of realistic sample values.
    private static T Pick<T>(IReadOnlyList<T> options) => options[Next() % options.Count];

    private static readonly string[] CompanyNames =
    [
        "Alpine Demolition",
        "J Browne Construction Company Limited",
        "Jones Bros Ruthin (Civil Engineering) Co Ltd",
        "Sweet Project Holdings Limited",
        "Travis Perkins Trading Company Limited (Keyline)"
    ];

    // site_name in the dev DB is "<postcode> - <location>"; these mirror real rows.
    private static readonly (string Postcode, string City, string Location)[] SitePlaces =
    [
        ("UB4 0SL", "Hayes", "Trinity Data Centre"),
        ("M30 7DR", "Manchester", "Peel Green Road"),
        ("LA12 9HW", "Kendal", "West End Farm"),
        ("TW6 1QG", "Hounslow", "Heathrow Airport - Terminal 3"),
        ("MK6 5LD", "Eaglestone", "Milton Keynes University Hospital")
    ];

    private static readonly (string Manufacturer, string Model, string Firmware)[] MonitorHardware =
    [
        ("Palas GmbH", "AQGuardSmart1000", "1.0.13"),
        ("Svantek", "SV307", "2.1.0"),
        ("Omnidots", "SWARM", "3.4.1")
    ];

    // Function summary: Builds a company with a realistic UK contractor name.
    public static Company Company(string? companyName = null, Guid? id = null)
    {
        return new Company
        {
            Id = id ?? Guid.NewGuid(),
            CompanyName = companyName ?? Pick(CompanyNames),
            Contracts = []
        };
    }

    // Function summary: Builds a contract with a six-digit number, on-hire date, and its owning company/site links.
    public static Contract Contract(
        Guid companyId,
        Guid? id = null,
        string? contractNumber = null,
        Guid? siteId = null,
        DateTime? onHireDate = null,
        DateTime? offHireDate = null)
    {
        return new Contract
        {
            Id = id ?? Guid.NewGuid(),
            ContractNumber = contractNumber ?? (225000 + Next()).ToString(System.Globalization.CultureInfo.InvariantCulture),
            CompanyId = companyId,
            SiteiD = siteId,
            OnHireDate = onHireDate ?? new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            OffHireDate = offHireDate
        };
    }

    // Function summary: Builds a site with an "<postcode> - <location>" name, UK address, and 08:00-18:00 hours.
    public static Site Site(
        string? siteName = null,
        Guid? id = null,
        DateTime? createDate = null,
        bool archived = false)
    {
        var place = Pick(SitePlaces);
        return new Site
        {
            Id = id ?? Guid.NewGuid(),
            SiteName = siteName ?? $"{place.Postcode} - {place.Location}",
            AddressLine1 = place.Location,
            City = place.City,
            Postcode = place.Postcode,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            CreateDate = createDate ?? DateTime.UtcNow,
            Archived = archived,
            Contracts = [],
            OperatingHours = []
        };
    }

    // Function summary: Builds a monitor of the requested type with realistic hardware and a five-digit serial.
    public static MonitorEntity Monitor(
        MonitorTypeEnum type = MonitorTypeEnum.Noise,
        Guid? id = null,
        string? serialId = null,
        string? fleetNr = null,
        string? manufacturer = null,
        string? model = null,
        DateTime? listedAtTime = null,
        DateTime? lastDataTime15Min = null)
    {
        var hardware = Pick(MonitorHardware);
        var index = Next();
        return new MonitorEntity
        {
            Id = id ?? Guid.NewGuid(),
            SerialId = serialId ?? (16000 + index).ToString(System.Globalization.CultureInfo.InvariantCulture),
            FleetNr = fleetNr ?? $"R{index}V",
            Manufacturer = manufacturer ?? hardware.Manufacturer,
            Model = model ?? hardware.Model,
            FirmwareVersion = hardware.Firmware,
            TypeOfMonitor = type,
            ListedAtTime = listedAtTime ?? DateTime.UtcNow.AddDays(-30),
            LastDataTime15Min = lastDataTime15Min ?? DateTime.UtcNow.AddMinutes(-5)
        };
    }

    // Function summary: Builds a deployment tying a monitor to a contract at a UK location.
    public static Deployment Deployment(
        Guid contractId,
        Guid monitorId,
        Guid? id = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        double lat = 51.5074,
        double lng = -0.1278,
        string? location = null,
        string? what3words = null)
    {
        return new Deployment
        {
            Id = id ?? Guid.NewGuid(),
            ContractId = contractId,
            MonitorId = monitorId,
            StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
            EndDate = endDate,
            Lat = lat,
            Lng = lng,
            Location = location ?? "Main Gate Entrance",
            What3words = what3words ?? "///decide.stacks.king"
        };
    }

    // Function summary: Builds an alert notification against a monitor.
    public static Notification Notification(
        Guid monitorId,
        Guid? id = null,
        DateTime? notificationTime = null,
        AlertTypeEnum alertType = AlertTypeEnum.Alert,
        string alertField = "LAeq",
        double level = 80.72,
        double limitOn = 75,
        int averagingPeriod = 3600,
        DateTime? closedTime = null)
    {
        return new Notification
        {
            Id = id ?? Guid.NewGuid(),
            MonitorId = monitorId,
            NotificationTime = notificationTime ?? DateTime.UtcNow.AddHours(-1),
            AlertType = alertType,
            AlertField = alertField,
            Level = level,
            LimitOn = limitOn,
            AveragingPeriod = averagingPeriod,
            ClosedTime = closedTime
        };
    }

    // Function summary: Builds an alert-level rule for a monitor (defaults to a weekday LAeq threshold).
    public static Alertlevel AlertLevel(
        Guid monitorId,
        string serialId,
        Guid? id = null,
        string alertField = "LAeq",
        AlertTypeEnum alertType = AlertTypeEnum.Alert,
        double limitOn = 75,
        double limitOff = 74.99,
        int averagingPeriod = 3600,
        bool isActive = true)
    {
        return new Alertlevel
        {
            Id = id ?? Guid.NewGuid(),
            MonitorId = monitorId,
            SerialId = serialId,
            AlertField = alertField,
            AlertType = alertType,
            LimitOn = limitOn,
            LimitOff = limitOff,
            AveragingPeriod = averagingPeriod,
            IsActive = isActive,
            Weekdays = true,
            Saturdays = false,
            Sundays = false,
            IsDeleted = false
        };
    }

    // Function summary: Assigns a user to a site.
    public static SiteUsers SiteUser(
        Guid siteId,
        Guid userId,
        Guid? id = null,
        DateTime? startDate = null,
        bool siteContact = false)
    {
        return new SiteUsers
        {
            Id = id ?? Guid.NewGuid(),
            SiteId = siteId,
            UserId = userId,
            StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
            SiteContact = siteContact
        };
    }
}
