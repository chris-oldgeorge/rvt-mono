// File summary: Supports RVT business-layer coordination and shared workflow helpers.
// Major updates:
// - 2026-07-09 pending Routed local/UTC conversion through the injected date-time provider.
// - 2026-06-26 pending Removed unused legacy deployment data path and split monitor data helpers for Sonar cleanup.
// - 2026-06-25 pending Initialized non-nullable Fourier/filter properties and guarded nullable deployment reads.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.

using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RVT.BusinessLogic;
using RVT.Entities.Querying;
using AForge.Math;
using System.Globalization;
using System.Threading;


namespace RvtPortal.Spa.Application.Monitors
{
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
        public Complex[] XVtopData { get; set; } = Array.Empty<Complex>();
        public Complex[] YVtopData { get; set; } = Array.Empty<Complex>();
        public Complex[] ZVtopData { get; set; } = Array.Empty<Complex>();

        // Function summary: Retrieves frequency data for callers.
        public double GetFrequency(int Index, int MeasurementDuration)
        {
            return (double)Index / (double)MeasurementDuration / (double)DataLength;
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
        private static readonly string DUST_MONITOR_NAME = "Air Quality Levels at Dust Monitor {0}";
        private static readonly string NOISE_MONITOR_NAME = "Sound Levels at Noise Monitor {0}";
        private static readonly string VIBRATION_MONITOR_NAME = "Vibration Levels at Vibration Monitor {0}";
        private static readonly string VIBRATION_TRACES_MONITOR_NAME = "Traces at Vibration Monitor {0}";

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
        public SearchQueryResult<OmnidotsTraces>? VibrationTraces { get; set; }

        // Function summary: Retrieves monitor name data for callers.
        private static string GetMonitorName(string? fleetNr, string? serialId, MonitorTypeEnum? typeOfMonitor, bool traces = false)
        {
            if (typeOfMonitor != null)
            {
                switch ((MonitorTypeEnum)typeOfMonitor)
                {
                    case MonitorTypeEnum.Dust:
                        return String.Format(CultureInfo.InvariantCulture, DUST_MONITOR_NAME, fleetNr ?? String.Format(CultureInfo.InvariantCulture, "SN {0}", serialId));
                    case MonitorTypeEnum.Noise:
                        return String.Format(CultureInfo.InvariantCulture, NOISE_MONITOR_NAME, fleetNr ?? String.Format(CultureInfo.InvariantCulture, "SN {0}", serialId));
                    case MonitorTypeEnum.Vibration:
                        return String.Format(CultureInfo.InvariantCulture, traces ? VIBRATION_TRACES_MONITOR_NAME : VIBRATION_MONITOR_NAME, fleetNr ?? String.Format(CultureInfo.InvariantCulture, "SN {0}", serialId));
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
        private static int GetAvrgDurationFromFilterOption(string? FilterOption, MonitorTypeEnum MonitorType)
        {
            int avrgDuration;

            if (int.TryParse(FilterOption, NumberStyles.Integer, CultureInfo.InvariantCulture, out avrgDuration))
            {
                return avrgDuration;
            }

            switch (MonitorType)
            {
                case MonitorTypeEnum.Dust:
                    return 60;
                case MonitorTypeEnum.Noise:
                    return 900;
                default:
                    return 0;
            }
        }

        // Function summary: Handles the prepare data for fourier transform workflow for this module.
        private static OmnidotsFourierData PrepareDataForFourierTransform(List<OmnidotsPeakLevel> OmnidotsPeakLevels, int MeasurementDuration, bool UseFastTransform)
        {
            int numEntries = CalculateFourierEntryCount(OmnidotsPeakLevels, MeasurementDuration, UseFastTransform);

            var fourierData = new OmnidotsFourierData
            {
                DataLength = numEntries,
                XVtopData = new Complex[numEntries],
                YVtopData = new Complex[numEntries],
                ZVtopData = new Complex[numEntries],
            };

            DateTime? lastTime = null;
            int index = 0;

            foreach (var vibrationLevel in OmnidotsPeakLevels)
            {
                index = FillMissingFourierSamples(fourierData, index, numEntries, MeasurementDuration, ref lastTime, vibrationLevel.SampleTime);
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
        private static int CalculateFourierEntryCount(List<OmnidotsPeakLevel> OmnidotsPeakLevels, int MeasurementDuration, bool UseFastTransform)
        {
            DateTime firstEntry = OmnidotsPeakLevels.First().SampleTime;
            DateTime lastEntry = OmnidotsPeakLevels.Last().SampleTime;
            int numEntries = (int)(lastEntry - firstEntry).TotalSeconds / MeasurementDuration;

            return UseFastTransform ? RoundUpToSupportedPowerOfTwo(numEntries) : numEntries;
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
        private static int FillMissingFourierSamples(OmnidotsFourierData fourierData, int index, int numEntries, int MeasurementDuration, ref DateTime? lastTime, DateTime sampleTime)
        {
            while (lastTime != null && (sampleTime - (DateTime)lastTime).TotalSeconds > MeasurementDuration)
            {
                if (index >= numEntries)
                {
                    break;
                }

                SetFourierSample(fourierData, index, 0, 0, 0);
                index++;
                lastTime = ((DateTime)lastTime).AddSeconds(MeasurementDuration);
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
        public static async Task<MonitorData> GetDeploymentData(IMonitorService monitorService, IRvtDateTimeProvider dateTimeProvider, Guid DeploymentId, Guid? TraceId, string? FilterOption, DateTime? FromDate, DateTime? ToDate, bool GraphData, int? Page = null, int? PageSize = null, string? Sort = null, OrderByDirectionEnum? SortDir = null)
        {
            Deployment deployment = (await monitorService.DeploymentReadOneAsync(DeploymentId))!;

            var defaultFromDate = DateTime.Today.AddDays(-1).LocalToUtc(dateTimeProvider);
            var defaultToDate = DateTime.Today.AddDays(1).LocalToUtc(dateTimeProvider);
            var (chosenFromDate, chosenToDate) = NormalizeDeploymentDateRange(FromDate, ToDate, defaultFromDate, defaultToDate);
            TimeSpan chosenTimeSpan = chosenToDate - chosenFromDate;
            var monitor = await monitorService.ReadOneAsync(deployment.MonitorId);

            if (TraceId != null)
            {
                return await BuildTraceMonitorData(monitorService, monitor, (Guid)TraceId, defaultFromDate, defaultToDate, FilterOption);
            }

            if (monitor == null)
            {
                return BuildEmptyMonitorData(chosenFromDate, chosenToDate, FilterOption);
            }

            var (minDate, maxDate) = GetDeploymentBounds(deployment, dateTimeProvider);
            var (validFromDate, validToDate) = ClampDeploymentDateRange(minDate, maxDate, chosenFromDate, chosenToDate, chosenTimeSpan, dateTimeProvider);
            var monitorData = BuildMonitorData(monitor, minDate, maxDate, validFromDate, validToDate, FilterOption);

            await ApplyMonitorReadings(monitorService, monitorData, monitor, validFromDate, validToDate, FilterOption, GraphData, Page, PageSize, Sort, SortDir);

            monitorData.FromDateChanged = FromDate != null && FromDate != monitorData.FromDate.TruncateSeconds();
            monitorData.ToDateChanged = ToDate != null && ToDate != monitorData.ToDate.TruncateSeconds();

            return monitorData;
        }

        // Function summary: Normalizes deployment date range input against the default window.
        private static (DateTime FromDate, DateTime ToDate) NormalizeDeploymentDateRange(DateTime? FromDate, DateTime? ToDate, DateTime defaultFromDate, DateTime defaultToDate)
        {
            var chosenFromDate = FromDate == null ? defaultFromDate : DateTime.SpecifyKind((DateTime)FromDate, DateTimeKind.Utc);
            var chosenToDate = ToDate == null ? defaultToDate : DateTime.SpecifyKind((DateTime)ToDate, DateTimeKind.Utc);

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
        private static async Task<MonitorData> BuildTraceMonitorData(IMonitorService monitorService, RVT.Entities.Monitor? monitor, Guid traceId, DateTime defaultFromDate, DateTime defaultToDate, string? FilterOption)
        {
            var traceIndex = await monitorService.TracesIndexReadOne(traceId);
            var traces = await monitorService.GetVibrationTraces(traceId);

            return new MonitorData
            {
                Monitor = monitor,
                MinDate = defaultFromDate,
                MaxDate = defaultToDate,
                FromDate = traceIndex?.StartTime != null ? traceIndex.StartTime : defaultFromDate,
                ToDate = traceIndex?.EndTime != null ? traceIndex.EndTime : defaultToDate,
                FilterOption = FilterOption,
                VibrationTraces = traces
            };
        }

        // Function summary: Builds an empty monitor data response when a deployment monitor is unavailable.
        private static MonitorData BuildEmptyMonitorData(DateTime chosenFromDate, DateTime chosenToDate, string? FilterOption)
        {
            return new MonitorData
            {
                MinDate = chosenFromDate,
                MaxDate = chosenToDate,
                FromDate = chosenFromDate,
                ToDate = chosenToDate,
                FilterOption = FilterOption,
            };
        }

        // Function summary: Calculates date bounds from the deployment.
        private static (DateTime MinDate, DateTime MaxDate) GetDeploymentBounds(Deployment deployment, IRvtDateTimeProvider dateTimeProvider)
        {
            var minDate = deployment.StartDate;
            var maxDate = deployment.EndDate ?? DateTime.Today.AddDays(2).LocalToUtc(dateTimeProvider).AddMilliseconds(-1);
            return (minDate, maxDate);
        }

        // Function summary: Clamps the selected date range to deployment bounds.
        private static (DateTime FromDate, DateTime ToDate) ClampDeploymentDateRange(DateTime minDate, DateTime maxDate, DateTime chosenFromDate, DateTime chosenToDate, TimeSpan chosenTimeSpan, IRvtDateTimeProvider dateTimeProvider)
        {
            var validFromDate = minDate > chosenFromDate ? minDate : chosenFromDate;
            var validToDate = maxDate < chosenToDate ? maxDate : chosenToDate;

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
        private static MonitorData BuildMonitorData(RVT.Entities.Monitor monitor, DateTime minDate, DateTime maxDate, DateTime validFromDate, DateTime validToDate, string? FilterOption)
        {
            return new MonitorData
            {
                Monitor = monitor,
                MinDate = minDate,
                MaxDate = maxDate,
                FromDate = validFromDate,
                ToDate = validToDate,
                FilterOption = FilterOption,
            };
        }

        // Function summary: Populates monitor-type specific readings.
        private static async Task ApplyMonitorReadings(IMonitorService monitorService, MonitorData monitorData, RVT.Entities.Monitor monitor, DateTime validFromDate, DateTime validToDate, string? FilterOption, bool GraphData, int? Page, int? PageSize, string? Sort, OrderByDirectionEnum? SortDir)
        {
            switch (monitor.TypeOfMonitor)
            {
                case MonitorTypeEnum.Dust:
                    await ApplyDustData(monitorService, monitorData, monitor, validFromDate, validToDate, FilterOption, GraphData, Page, PageSize, Sort, SortDir);
                    break;
                case MonitorTypeEnum.Noise:
                    await ApplyNoiseData(monitorService, monitorData, monitor, validFromDate, validToDate, FilterOption, GraphData, Page, PageSize, Sort, SortDir);
                    break;
                case MonitorTypeEnum.Vibration:
                    await ApplyVibrationData(monitorService, monitorData, monitor, validFromDate, validToDate, FilterOption, GraphData, Page, PageSize, Sort, SortDir);
                    break;
            }
        }

        // Function summary: Applies maximum duration constraints and returns the adjusted end date.
        private static DateTime ApplyMaxDuration(MonitorData monitorData, DateTime validFromDate, DateTime validToDate, int maxDurationDays, bool GraphData)
        {
            if (!GraphData)
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
        private static async Task ApplyDustData(IMonitorService monitorService, MonitorData monitorData, RVT.Entities.Monitor monitor, DateTime validFromDate, DateTime validToDate, string? FilterOption, bool GraphData, int? Page, int? PageSize, string? Sort, OrderByDirectionEnum? SortDir)
        {
            int avrgDuration = GetAvrgDurationFromFilterOption(FilterOption, MonitorTypeEnum.Dust);
            validToDate = ApplyMaxDuration(monitorData, validFromDate, validToDate, avrgDuration / 5, GraphData);

            monitorData.DustLevels = avrgDuration == 28800
                ? await monitorService.GetMyAtmDustLevels8hourAvg(monitor.SerialId, validFromDate, validToDate, Page, PageSize, Sort, SortDir)
                : await monitorService.GetMyAtmDustLevels(monitor.SerialId, validFromDate, validToDate, avrgDuration, Page, PageSize, Sort, SortDir);

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
        private static async Task ApplyNoiseData(IMonitorService monitorService, MonitorData monitorData, RVT.Entities.Monitor monitor, DateTime validFromDate, DateTime validToDate, string? FilterOption, bool GraphData, int? Page, int? PageSize, string? Sort, OrderByDirectionEnum? SortDir)
        {
            int avrgDuration = GetAvrgDurationFromFilterOption(FilterOption, MonitorTypeEnum.Noise);
            int maxDurationDays = (FilterOption == "site" ? 86400 : avrgDuration) / 5;
            validToDate = ApplyMaxDuration(monitorData, validFromDate, validToDate, maxDurationDays, GraphData);

            monitorData.NoiseLevels = FilterOption switch
            {
                "site" => await monitorService.GetAirQnoiseLevelsSiteAvg(monitor.SerialId, validFromDate, validToDate, Page, PageSize, Sort, SortDir),
                _ when avrgDuration == 3600 => await monitorService.GetAirQnoiseLevels1hourAvg(monitor.SerialId, validFromDate, validToDate, Page, PageSize, Sort, SortDir),
                _ when avrgDuration == 86400 => await monitorService.GetAirQnoiseLevels1dayAvg(monitor.SerialId, validFromDate, validToDate, Page, PageSize, Sort, SortDir),
                _ => await monitorService.GetAirQnoiseLevels(monitor.SerialId, validFromDate, validToDate, Page, PageSize, Sort, SortDir)
            };

            monitorData.FilterOptions = new Dictionary<string, string>
            {
                { "900", "All Readings" },
                { "3600", "Hourly Averages" },
                { "86400", "Daily Averages" },
                { "site", "Site Averages" }
            };

            monitorData.FilterOption = FilterOption == "site" ? FilterOption : avrgDuration.ToString(CultureInfo.InvariantCulture);
        }

        // Function summary: Populates vibration readings or frequency data and filter options.
        private static async Task ApplyVibrationData(IMonitorService monitorService, MonitorData monitorData, RVT.Entities.Monitor monitor, DateTime validFromDate, DateTime validToDate, string? FilterOption, bool GraphData, int? Page, int? PageSize, string? Sort, OrderByDirectionEnum? SortDir)
        {
            monitorData.FilterOption = FilterOption ?? "time";
            validToDate = ApplyMaxDuration(monitorData, validFromDate, validToDate, FilterOption == "frequency" ? 1 : 3, GraphData);
            var vibrationLevels = await monitorService.GetOmnidotsPeakLevels(monitor.SerialId, validFromDate, validToDate, Page, PageSize, Sort, SortDir);

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
            var fourierData = PrepareDataForFourierTransform(vibrationLevels.Value, measurementDuration, true);
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
            var frequencyMagnitudes = new List<OmnidotsFrequencyMagnitudes>();
            for (var i = 0; i < fourierData.DataLength / 2; i++)
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
}
