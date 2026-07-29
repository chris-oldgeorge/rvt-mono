// File summary: Supports RVT business-layer coordination and shared workflow helpers.
// Major updates:
// - 2026-07-22 Represented vibration traces with the mapped OmnidotsTrace entity.
// - 2026-07-09 pending Routed local/UTC conversion through the injected date-time provider.
// - 2026-06-26 pending Removed unused legacy deployment data path and split monitor data helpers for Sonar cleanup.
// - 2026-06-25 pending Initialized non-nullable Fourier/filter properties and guarded nullable deployment reads.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using AForge.Math;
using RVT.BusinessLogic;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RVT.Entities.Querying;


namespace RvtPortal.Spa.Application.Monitors;

public class OmnidotsFrequencyMagnitudes
{
    public double Frequency { get; set; }
    public double? XVtop { get; set; }
    public double? YVtop { get; set; }
    public double? ZVtop { get; set; }
}

public class OmnidotsFourierData
{
    public int DataLength { get; set; }
    public DateTime? EndDate { get; set; }
    public Complex[] XVtopData { get; set; } = [];
    public Complex[] YVtopData { get; set; } = [];
    public Complex[] ZVtopData { get; set; } = [];

    // Function summary: Retrieves frequency data for callers.
    public double GetFrequency(int index, int measurementDuration)
    {
        return index / (double)measurementDuration / DataLength;
    }

    // Function summary: Retrieves magnitude data for callers.
    public static double GetMagnitude(Complex complex)
    {
        return Math.Sqrt(Math.Pow(complex.Re, 2) + Math.Pow(complex.Im, 2));
    }

    // Function summary: Retrieves phase data for callers.
    public static double GetPhase(Complex complex)
    {
        return Math.Atan2(complex.Im, complex.Re);
    }
}

public class MonitorData
{
    private sealed record MonitorPaging(
        int? Page,
        int? PageSize,
        string? Sort,
        OrderByDirectionEnum? SortDirection);

    private sealed record MonitorReadOptions(
        DateTime FromDate,
        DateTime ToDate,
        string? FilterOption,
        bool GraphData,
        MonitorPaging Paging);

    private static readonly string dustMonitorName = "Air Quality Levels at Dust Monitor {0}";
    private static readonly string noiseMonitorName = "Sound Levels at Noise Monitor {0}";
    private static readonly string vibrationMonitorName = "Vibration Levels at Vibration Monitor {0}";
    private static readonly string vibrationTracesMonitorName = "Traces at Vibration Monitor {0}";

    public RVT.Entities.Monitor? Monitor { get; set; }
    public DateTime MinDate { get; set; }
    public DateTime MaxDate { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public bool FromDateChanged { get; set; }
    public bool ToDateChanged { get; set; }
    public TimeSpan? MaxDuration { get; set; }
    public Dictionary<string, string> FilterOptions { get; set; } = new();
    public string? FilterOption { get; set; }
    public SearchQueryResult<MyAtmDustLevel>? DustLevels { get; set; }
    public SearchQueryResult<NoiseLevel15minAvg>? NoiseLevels { get; set; }
    public SearchQueryResult<OmnidotsPeakLevel>? VibrationLevels { get; set; }
    public List<OmnidotsFrequencyMagnitudes>? VibrationFrequencyMagnitudes { get; set; }
    public SearchQueryResult<OmnidotsTrace>? VibrationTraces { get; set; }

    // Function summary: Retrieves monitor name data for callers.
    private static string GetMonitorName(string? fleetNr, string? serialId, MonitorTypeEnum? typeOfMonitor, bool traces = false)
    {
        if (typeOfMonitor != null)
        {
            switch ((MonitorTypeEnum)typeOfMonitor)
            {
                case MonitorTypeEnum.Dust:
                    return string.Format(CultureInfo.InvariantCulture, dustMonitorName, fleetNr ?? string.Format(CultureInfo.InvariantCulture, "SN {0}", serialId));
                case MonitorTypeEnum.Noise:
                    return string.Format(CultureInfo.InvariantCulture, noiseMonitorName, fleetNr ?? string.Format(CultureInfo.InvariantCulture, "SN {0}", serialId));
                case MonitorTypeEnum.Vibration:
                    return string.Format(CultureInfo.InvariantCulture, traces ? vibrationTracesMonitorName : vibrationMonitorName, fleetNr ?? string.Format(CultureInfo.InvariantCulture, "SN {0}", serialId));
                default:
                    break;
            }
        }
        return fleetNr ?? "";
    }
    // Function summary: Retrieves monitor name data for callers.
    public static string GetMonitorName(RVT.Entities.Monitor? monitor, bool traces = false)
    {
        return GetMonitorName(monitor?.FleetNr, monitor?.SerialId, monitor?.TypeOfMonitor, traces);
    }
    // Function summary: Retrieves monitor name data for callers.
    public static string GetMonitorName(MonitorSearch? monitor, bool traces = false)
    {
        return GetMonitorName(monitor?.FleetNr, monitor?.SerialId, (MonitorTypeEnum?)monitor?.TypeOfMonitor, traces);
    }

    // Function summary: Retrieves avrg duration from filter option data for callers.
    private static int GetAvrgDurationFromFilterOption(string? filterOption, MonitorTypeEnum monitorType)
    {

        if (int.TryParse(filterOption, NumberStyles.Integer, CultureInfo.InvariantCulture, out int avrgDuration))
        {
            return avrgDuration;
        }

        return monitorType switch
        {
            MonitorTypeEnum.Dust => 60,
            MonitorTypeEnum.Noise => 900,
            MonitorTypeEnum.Vibration => throw new NotImplementedException(),
            _ => 0,
        };
    }

    // Function summary: Handles the prepare data for fourier transform workflow for this module.
    private static OmnidotsFourierData PrepareDataForFourierTransform(List<OmnidotsPeakLevel> omnidotsPeakLevels, int measurementDuration, bool useFastTransform)
    {
        int numEntries = CalculateFourierEntryCount(omnidotsPeakLevels, measurementDuration, useFastTransform);

        OmnidotsFourierData fourierData = new()
        {
            DataLength = numEntries,
            XVtopData = new Complex[numEntries],
            YVtopData = new Complex[numEntries],
            ZVtopData = new Complex[numEntries],
        };

        DateTime? lastTime = null;
        int index = 0;

        foreach (OmnidotsPeakLevel vibrationLevel in omnidotsPeakLevels)
        {
            index = FillMissingFourierSamples(fourierData, index, numEntries, measurementDuration, ref lastTime, vibrationLevel.SampleTime);
            if (index >= numEntries)
            {
                fourierData.EndDate = vibrationLevel.SampleTime;
                break;
            }
            SetFourierSample(fourierData, index, vibrationLevel);
            index++;
            lastTime = vibrationLevel.SampleTime;
        }

        return fourierData;
    }

    // Function summary: Calculates the Fourier buffer length required for the selected transform.
    private static int CalculateFourierEntryCount(List<OmnidotsPeakLevel> omnidotsPeakLevels, int measurementDuration, bool useFastTransform)
    {
        DateTime firstEntry = omnidotsPeakLevels.First().SampleTime;
        DateTime lastEntry = omnidotsPeakLevels.Last().SampleTime;
        int numEntries = (int)(lastEntry - firstEntry).TotalSeconds / measurementDuration;

        return useFastTransform ? RoundUpToSupportedPowerOfTwo(numEntries) : numEntries;
    }

    // Function summary: Rounds a Fourier buffer length up to the closest supported FFT power of two.
    private static int RoundUpToSupportedPowerOfTwo(int numEntries)
    {
        const int maxPowerOfTwo = 20;
        for (int i = 1; i <= maxPowerOfTwo; i++)
        {
            int x = (int)Math.Pow(2, i);
            if (x >= numEntries || i == maxPowerOfTwo)
            {
                return x;
            }
        }

        return numEntries;
    }

    // Function summary: Fills missing Fourier data points with zero samples until the next measurement time.
    private static int FillMissingFourierSamples(OmnidotsFourierData fourierData, int index, int numEntries, int measurementDuration, ref DateTime? lastTime, DateTime sampleTime)
    {
        while (lastTime != null && (sampleTime - (DateTime)lastTime).TotalSeconds > measurementDuration)
        {
            if (index >= numEntries)
            {
                break;
            }

            SetFourierSample(fourierData, index, 0, 0, 0);
            index++;
            lastTime = ((DateTime)lastTime).AddSeconds(measurementDuration);
        }

        return index;
    }

    // Function summary: Copies a vibration reading into the Fourier data arrays.
    private static void SetFourierSample(OmnidotsFourierData fourierData, int index, OmnidotsPeakLevel vibrationLevel)
    {
        SetFourierSample(fourierData, index, vibrationLevel.Xvtop ?? 0, vibrationLevel.Yvtop ?? 0, vibrationLevel.Zvtop ?? 0);
    }

    // Function summary: Copies explicit values into the Fourier data arrays.
    private static void SetFourierSample(OmnidotsFourierData fourierData, int index, double xvtop, double yvtop, double zvtop)
    {
        fourierData.XVtopData[index] = new Complex(xvtop, 0);
        fourierData.YVtopData[index] = new Complex(yvtop, 0);
        fourierData.ZVtopData[index] = new Complex(zvtop, 0);
    }


    // Function summary: Retrieves deployment data data for callers.
    [SuppressMessage(
        "Maintainability",
        "S107:Methods should not have too many parameters",
        Justification = "This public compatibility boundary preserves existing deployment-data callers; internal dispatch uses cohesive option objects.")]
    public static async Task<MonitorData> GetDeploymentData(IMonitorService monitorService, IRvtDateTimeProvider dateTimeProvider, Guid deploymentId, Guid? traceId, string? filterOption, DateTime? fromDate, DateTime? toDate, bool graphData, int? page = null, int? pageSize = null, string? sort = null, OrderByDirectionEnum? sortDir = null)
    {
        Deployment deployment = (await monitorService.DeploymentReadOneAsync(deploymentId))!;

        DateTime defaultFromDate = DateTime.Today.AddDays(-1).LocalToUtc(dateTimeProvider);
        DateTime defaultToDate = DateTime.Today.AddDays(1).LocalToUtc(dateTimeProvider);
        (DateTime chosenFromDate, DateTime chosenToDate) = NormalizeDeploymentDateRange(fromDate, toDate, defaultFromDate, defaultToDate);
        TimeSpan chosenTimeSpan = chosenToDate - chosenFromDate;
        RVT.Entities.Monitor? monitor = await monitorService.ReadOneAsync(deployment.MonitorId);

        if (traceId != null)
        {
            return await BuildTraceMonitorData(monitorService, monitor, (Guid)traceId, defaultFromDate, defaultToDate, filterOption);
        }

        if (monitor == null)
        {
            return BuildEmptyMonitorData(chosenFromDate, chosenToDate, filterOption);
        }

        (DateTime minDate, DateTime maxDate) = GetDeploymentBounds(deployment, dateTimeProvider);
        (DateTime validFromDate, DateTime validToDate) = ClampDeploymentDateRange(minDate, maxDate, chosenFromDate, chosenToDate, chosenTimeSpan, dateTimeProvider);
        MonitorData monitorData = BuildMonitorData(monitor, minDate, maxDate, validFromDate, validToDate, filterOption);

        MonitorReadOptions readOptions = new(
            validFromDate,
            validToDate,
            filterOption,
            graphData,
            new MonitorPaging(page, pageSize, sort, sortDir));
        await ApplyMonitorReadings(monitorService, monitorData, monitor, readOptions);

        monitorData.FromDateChanged = fromDate != null && fromDate != monitorData.FromDate.TruncateSeconds();
        monitorData.ToDateChanged = toDate != null && toDate != monitorData.ToDate.TruncateSeconds();

        return monitorData;
    }

    // Function summary: Normalizes deployment date range input against the default window.
    private static (DateTime FromDate, DateTime ToDate) NormalizeDeploymentDateRange(DateTime? fromDate, DateTime? toDate, DateTime defaultFromDate, DateTime defaultToDate)
    {
        DateTime chosenFromDate = fromDate == null ? defaultFromDate : DateTime.SpecifyKind((DateTime)fromDate, DateTimeKind.Utc);
        DateTime chosenToDate = toDate == null ? defaultToDate : DateTime.SpecifyKind((DateTime)toDate, DateTimeKind.Utc);

        if (chosenToDate > chosenFromDate)
        {
            return (chosenFromDate, chosenToDate);
        }

        chosenToDate = defaultToDate;
        if (chosenToDate <= chosenFromDate)
        {
            chosenFromDate = defaultFromDate;
        }

        return (chosenFromDate, chosenToDate);
    }

    // Function summary: Builds monitor data for a vibration trace request.
    private static async Task<MonitorData> BuildTraceMonitorData(IMonitorService monitorService, RVT.Entities.Monitor? monitor, Guid traceId, DateTime defaultFromDate, DateTime defaultToDate, string? filterOption)
    {
        OmnidotsTracesIndex? traceIndex = await monitorService.TracesIndexReadOne(traceId);
        SearchQueryResult<OmnidotsTrace> traces = await monitorService.GetVibrationTraces(traceId);

        return new MonitorData
        {
            Monitor = monitor,
            MinDate = defaultFromDate,
            MaxDate = defaultToDate,
            FromDate = traceIndex?.StartTime != null ? traceIndex.StartTime : defaultFromDate,
            ToDate = traceIndex?.EndTime != null ? traceIndex.EndTime : defaultToDate,
            FilterOption = filterOption,
            VibrationTraces = traces
        };
    }

    // Function summary: Builds an empty monitor data response when a deployment monitor is unavailable.
    private static MonitorData BuildEmptyMonitorData(DateTime chosenFromDate, DateTime chosenToDate, string? filterOption)
    {
        return new MonitorData
        {
            MinDate = chosenFromDate,
            MaxDate = chosenToDate,
            FromDate = chosenFromDate,
            ToDate = chosenToDate,
            FilterOption = filterOption,
        };
    }

    // Function summary: Calculates date bounds from the deployment.
    private static (DateTime MinDate, DateTime MaxDate) GetDeploymentBounds(Deployment deployment, IRvtDateTimeProvider dateTimeProvider)
    {
        DateTime minDate = deployment.StartDate;
        DateTime maxDate = deployment.EndDate ?? DateTime.Today.AddDays(2).LocalToUtc(dateTimeProvider).AddMilliseconds(-1);
        return (minDate, maxDate);
    }

    // Function summary: Clamps the selected date range to deployment bounds.
    private static (DateTime FromDate, DateTime ToDate) ClampDeploymentDateRange(DateTime minDate, DateTime maxDate, DateTime chosenFromDate, DateTime chosenToDate, TimeSpan chosenTimeSpan, IRvtDateTimeProvider dateTimeProvider)
    {
        DateTime validFromDate = minDate > chosenFromDate ? minDate : chosenFromDate;
        DateTime validToDate = maxDate < chosenToDate ? maxDate : chosenToDate;

        if (validToDate > validFromDate)
        {
            return (validFromDate, validToDate);
        }

        validFromDate = minDate;
        validToDate = maxDate;
        if (validToDate - validFromDate > chosenTimeSpan)
        {
            validToDate = (validFromDate + chosenTimeSpan).Date.LocalToUtc(dateTimeProvider);
        }

        return (validFromDate, validToDate);
    }

    // Function summary: Creates monitor data with shared date metadata.
    private static MonitorData BuildMonitorData(RVT.Entities.Monitor monitor, DateTime minDate, DateTime maxDate, DateTime validFromDate, DateTime validToDate, string? filterOption)
    {
        return new MonitorData
        {
            Monitor = monitor,
            MinDate = minDate,
            MaxDate = maxDate,
            FromDate = validFromDate,
            ToDate = validToDate,
            FilterOption = filterOption,
        };
    }

    // Function summary: Populates monitor-type specific readings.
    private static async Task ApplyMonitorReadings(
        IMonitorService monitorService,
        MonitorData monitorData,
        RVT.Entities.Monitor monitor,
        MonitorReadOptions options)
    {
        switch (monitor.TypeOfMonitor)
        {
            case MonitorTypeEnum.Dust:
                await ApplyDustData(monitorService, monitorData, monitor, options);
                break;
            case MonitorTypeEnum.Noise:
                await ApplyNoiseData(monitorService, monitorData, monitor, options);
                break;
            case MonitorTypeEnum.Vibration:
                await ApplyVibrationData(monitorService, monitorData, monitor, options);
                break;
            default:
                break;
        }
    }

    // Function summary: Applies maximum duration constraints and returns the adjusted end date.
    private static DateTime ApplyMaxDuration(MonitorData monitorData, DateTime validFromDate, DateTime validToDate, int maxDurationDays, bool graphData)
    {
        if (!graphData)
        {
            maxDurationDays *= 2;
        }

        monitorData.MaxDuration = new TimeSpan(maxDurationDays, 0, 0, 0);
        if (validToDate - validFromDate > monitorData.MaxDuration)
        {
            monitorData.ToDate = validFromDate + monitorData.MaxDuration.Value;
            return monitorData.ToDate;
        }

        return validToDate;
    }

    // Function summary: Populates dust readings and filter options.
    private static async Task ApplyDustData(
        IMonitorService monitorService,
        MonitorData monitorData,
        RVT.Entities.Monitor monitor,
        MonitorReadOptions options)
    {
        int avrgDuration = GetAvrgDurationFromFilterOption(options.FilterOption, MonitorTypeEnum.Dust);
        DateTime validToDate = ApplyMaxDuration(
            monitorData,
            options.FromDate,
            options.ToDate,
            avrgDuration / 5,
            options.GraphData);

        monitorData.DustLevels = avrgDuration == 28800
            ? await monitorService.GetMyAtmDustLevels8hourAvg(monitor.SerialId, options.FromDate, validToDate, options.Paging.Page, options.Paging.PageSize, options.Paging.Sort, options.Paging.SortDirection)
            : await monitorService.GetMyAtmDustLevels(monitor.SerialId, options.FromDate, validToDate, avrgDuration, options.Paging.Page, options.Paging.PageSize, options.Paging.Sort, options.Paging.SortDirection);

        monitorData.FilterOptions = new Dictionary<string, string>
        {
            { "60", "All Readings" },
            { "900", "15 Min Averages" },
            { "3600", "Hourly Averages" },
            { "28800", "8 Hour Averages" },
            { "86400", "Daily Averages" }
        };

        monitorData.FilterOption = avrgDuration.ToString(CultureInfo.InvariantCulture);
    }

    // Function summary: Populates noise readings and filter options.
    private static async Task ApplyNoiseData(
        IMonitorService monitorService,
        MonitorData monitorData,
        RVT.Entities.Monitor monitor,
        MonitorReadOptions options)
    {
        int avrgDuration = GetAvrgDurationFromFilterOption(options.FilterOption, MonitorTypeEnum.Noise);
        int maxDurationDays = (options.FilterOption == "site" ? 86400 : avrgDuration) / 5;
        DateTime validToDate = ApplyMaxDuration(
            monitorData,
            options.FromDate,
            options.ToDate,
            maxDurationDays,
            options.GraphData);

        monitorData.NoiseLevels = options.FilterOption switch
        {
            "site" => await monitorService.GetAirQnoiseLevelsSiteAvg(monitor.SerialId, options.FromDate, validToDate, options.Paging.Page, options.Paging.PageSize, options.Paging.Sort, options.Paging.SortDirection),
            _ when avrgDuration == 3600 => await monitorService.GetAirQnoiseLevels1hourAvg(monitor.SerialId, options.FromDate, validToDate, options.Paging.Page, options.Paging.PageSize, options.Paging.Sort, options.Paging.SortDirection),
            _ when avrgDuration == 86400 => await monitorService.GetAirQnoiseLevels1dayAvg(monitor.SerialId, options.FromDate, validToDate, options.Paging.Page, options.Paging.PageSize, options.Paging.Sort, options.Paging.SortDirection),
            _ => await monitorService.GetAirQnoiseLevels(monitor.SerialId, options.FromDate, validToDate, options.Paging.Page, options.Paging.PageSize, options.Paging.Sort, options.Paging.SortDirection)
        };

        monitorData.FilterOptions = new Dictionary<string, string>
        {
            { "900", "All Readings" },
            { "3600", "Hourly Averages" },
            { "86400", "Daily Averages" },
            { "site", "Site Averages" }
        };

        monitorData.FilterOption = options.FilterOption == "site"
            ? options.FilterOption
            : avrgDuration.ToString(CultureInfo.InvariantCulture);
    }

    // Function summary: Populates vibration readings or frequency data and filter options.
    private static async Task ApplyVibrationData(
        IMonitorService monitorService,
        MonitorData monitorData,
        RVT.Entities.Monitor monitor,
        MonitorReadOptions options)
    {
        monitorData.FilterOption = options.FilterOption ?? "time";
        DateTime validToDate = ApplyMaxDuration(
            monitorData,
            options.FromDate,
            options.ToDate,
            options.FilterOption == "frequency" ? 1 : 3,
            options.GraphData);
        SearchQueryResult<OmnidotsPeakLevel> vibrationLevels = await monitorService.GetOmnidotsPeakLevels(
            monitor.SerialId,
            options.FromDate,
            validToDate,
            options.Paging.Page,
            options.Paging.PageSize,
            options.Paging.Sort,
            options.Paging.SortDirection);

        if (monitorData.FilterOption == "frequency")
        {
            await ApplyVibrationFrequencyData(monitorService, monitorData, monitor.SerialId, vibrationLevels);
        }
        else
        {
            monitorData.VibrationLevels = vibrationLevels;
        }

        monitorData.FilterOptions = new Dictionary<string, string>
        {
            { "time", "Over Time" },
            { "frequency", "By Frequency" }
        };
    }

    // Function summary: Builds vibration frequency magnitudes from peak levels.
    private static async Task ApplyVibrationFrequencyData(IMonitorService monitorService, MonitorData monitorData, string serialId, SearchQueryResult<OmnidotsPeakLevel> vibrationLevels)
    {
        OmnidotsMonitorStatus? monitorStatus = await monitorService.GetVibrationMonitorStatusAsync(serialId);
        if (monitorStatus?.MeasurementDuration == null || vibrationLevels.Value.Count == 0)
        {
            return;
        }

        int measurementDuration = (int)monitorStatus.MeasurementDuration;
        OmnidotsFourierData fourierData = PrepareDataForFourierTransform(vibrationLevels.Value, measurementDuration, true);
        FourierTransform.FFT(fourierData.XVtopData, FourierTransform.Direction.Forward);
        FourierTransform.FFT(fourierData.YVtopData, FourierTransform.Direction.Forward);
        FourierTransform.FFT(fourierData.ZVtopData, FourierTransform.Direction.Forward);

        monitorData.VibrationFrequencyMagnitudes = BuildFrequencyMagnitudes(fourierData, measurementDuration);

        if (fourierData.EndDate != null)
        {
            monitorData.ToDate = (DateTime)fourierData.EndDate;
        }
    }

    // Function summary: Converts Fourier data into chart-ready magnitudes.
    private static List<OmnidotsFrequencyMagnitudes> BuildFrequencyMagnitudes(OmnidotsFourierData fourierData, int measurementDuration)
    {
        List<OmnidotsFrequencyMagnitudes> frequencyMagnitudes = new();
        for (int i = 0; i < fourierData.DataLength / 2; i++)
        {
            frequencyMagnitudes.Add(new OmnidotsFrequencyMagnitudes
            {
                Frequency = fourierData.GetFrequency(i, measurementDuration),
                XVtop = OmnidotsFourierData.GetMagnitude(fourierData.XVtopData[i]),
                YVtop = OmnidotsFourierData.GetMagnitude(fourierData.YVtopData[i]),
                ZVtop = OmnidotsFourierData.GetMagnitude(fourierData.ZVtopData[i]),
            });
        }

        return frequencyMagnitudes;
    }

}
