namespace MyAtm.Api.Db.EntityFramework;

public sealed class MyAtmDustLevelEntity
{
    public string SerialId { get; set; } = string.Empty;
    public int Avrg { get; set; }
    public DateTime SampleTime { get; set; }
    public double? Pm1 { get; set; }
    public double? Pm2_5 { get; set; }
    public double? Pm10 { get; set; }
    public double? PmTotal { get; set; }
    public double? Weather_t { get; set; }
    public double? Weather_p { get; set; }
    public double? Weather_rh { get; set; }
}

public sealed class MyAtmAccessoryInfoEntity
{
    public string SerialId { get; set; } = string.Empty;
    public DateTime SampleTime { get; set; }
    public double? OperatingSpanPointDeviation { get; set; }
    public double? OperatingTLed { get; set; }
    public double? OperatingTHeating { get; set; }
    public double? OperatingVolumeFlow { get; set; }
    public double? OperatingVolumeFlowSignalLength { get; set; }
    public long OperatingVolumeFlowTimestamp { get; set; }
    public double? OperatingPeakPosition15s { get; set; }
    public double? OperatingVelocity { get; set; }
    public double? OperatingSlaNoiseLevel { get; set; }
    public double? OperatingSlaOffsetAdjustmentVoltage { get; set; }
    public double? OperatingTMio { get; set; }
    public double? OperatingPMio { get; set; }
    public double? OperatingRHMio { get; set; }
    public double? OperatingAutoCalibrationPeakPosition { get; set; }
    public double? OperatingPowerLed { get; set; }
    public double? OperatingPowerPmt { get; set; }
    public double? OperatingPowerHeating { get; set; }
    public double? OperatingPowerVolumeFlowBlower { get; set; }
    public double? OperatingPowerHousingBlower { get; set; }
    public double? OperatingPowerSeparatorBlower { get; set; }
    public double? OperatingFlowCorrectionFactor { get; set; }
    public bool DigitalCalibrationEnableStatus { get; set; }
    public bool DigitalIadsConnected { get; set; }
    public bool DigitalIadsActivated { get; set; }
    public bool DigitalAmbientProtectionAttached { get; set; }
    public bool DigitalCoincidence { get; set; }
    public bool DigitalWeatherStation { get; set; }
    public bool DigitalOperatingModus { get; set; }
    public bool DigitalVolumeFlow { get; set; }
    public bool DigitalSuction { get; set; }
    public bool DigitalIads { get; set; }
    public bool DigitalCalibration { get; set; }
    public bool DigitalSensorLed { get; set; }
    public bool DigitalSensorData { get; set; }
    public bool DigitalSensorNoise { get; set; }
    public bool DigitalCountModus { get; set; }
    public bool DigitalLiquidPumps { get; set; }
    public bool DigitalCondensationCooling { get; set; }
    public bool DigitalDropletSize { get; set; }
    public bool DigitalOpticsTemperature { get; set; }
    public bool DigitalGlobalWarning { get; set; }
    public bool DigitalGlobalError { get; set; }
    public bool DigitalEvaporationHeating { get; set; }
}

public sealed class MyAtmErrorMessageEntity
{
    public string Tag { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public DateTime ErrorTime { get; set; }
}

public sealed class MyAtmAlertOccurrenceEntity
{
    public string? OccurrenceKey { get; set; }
    public Guid NotificationId { get; set; }
    public Guid MonitorId { get; set; }
    public Guid RuleId { get; set; }
    public int Period { get; set; }
    public int AlertType { get; set; }
    public string Field { get; set; } = string.Empty;
    public double Level { get; set; }
    public DateTime TriggeredAt { get; set; }
    public bool IsSuppressed { get; set; }
    public DateTime CreatedAt { get; set; }
}
