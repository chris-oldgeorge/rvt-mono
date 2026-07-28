
using MyAtm.Model.Json;

namespace MyAtm.Model.Dto;


public class AccessoryInfoDto(string serialId, AccessoryInfo a)
{
    public string SerialId { get; } = serialId;
    public DateTime SampleTime { get; } = a.Timestamp;
    public Double? OperatingSpanPointDeviation { get; } = a.OperatingSpanPointDeviation;
    public Double? OperatingTLed { get; } = a.OperatingTLed;
    public Double? OperatingTHeating { get; } = a.OperatingTHeating;
    public Double? OperatingVolumeFlow { get; } = a.OperatingVolumeFlow;
    public Double? OperatingVolumeFlowSignalLength { get; } = a.OperatingVolumeFlowSignalLength;
    public long OperatingVolumeFlowTimestamp { get; } = a.OperatingVolumeFlowTimestamp;
    public Double? OperatingPeakPosition15s { get; } = a.OperatingPeakPosition15s;
    public Double? OperatingVelocity { get; } = a.OperatingVelocity;
    public Double? OperatingSlaNoiseLevel { get; } = a.OperatingSlaNoiseLevel;
    public Double? OperatingSlaOffsetAdjustmentVoltage { get; } = a.OperatingSlaOffsetAdjustmentVoltage;
    public Double? OperatingTMio { get; } = a.OperatingTMio;
    public Double? OperatingPMio { get; } = a.OperatingPMio;
    public Double? OperatingRHMio { get; } = a.OperatingRHMio;
    public Double? OperatingAutoCalibrationPeakPosition { get; } = a.OperatingAutoCalibrationPeakPosition;
    public Double? OperatingPowerLed { get; } = a.OperatingPowerLed;
    public Double? OperatingPowerPmt { get; } = a.OperatingPowerPmt;
    public Double? OperatingPowerHeating { get; } = a.OperatingPowerHeating;
    public Double? OperatingPowerVolumeFlowBlower { get; } = a.OperatingPowerVolumeFlowBlower;
    public Double? OperatingPowerHousingBlower { get; } = a.OperatingPowerHousingBlower;
    public Double? OperatingPowerSeparatorBlower { get; } = a.OperatingPowerSeparatorBlower;
    public Double? OperatingFlowCorrectionFactor { get; } = a.OperatingFlowCorrectionFactor;
    public bool DigitalCalibrationEnableStatus { get; } = a.DigitalCalibrationEnableStatus;
    public bool DigitalIadsConnected { get; } = a.DigitalIadsConnected;
    public bool DigitalIadsActivated { get; } = a.DigitalIadsActivated;
    public bool DigitalAmbientProtectionAttached { get; } = a.DigitalAmbientProtectionAttached;
    public bool DigitalCoincidence { get; } = a.DigitalCoincidence;
    public bool DigitalWeatherStation { get; } = a.DigitalWeatherStation;
    public bool DigitalOperatingModus { get; } = a.DigitalOperatingModus;
    public bool DigitalVolumeFlow { get; } = a.DigitalVolumeFlow;
    public bool DigitalSuction { get; } = a.DigitalSuction;
    public bool DigitalIads { get; } = a.DigitalIads;
    public bool DigitalCalibration { get; } = a.DigitalCalibration;
    public bool DigitalSensorLed { get; } = a.DigitalSensorLed;
    public bool DigitalSensorData { get; } = a.DigitalSensorData;
    public bool DigitalSensorNoise { get; } = a.DigitalSensorNoise;
    public bool DigitalCountModus { get; } = a.DigitalCountModus;
    public bool DigitalLiquidPumps { get; } = a.DigitalLiquidPumps;
    public bool DigitalCondensationCooling { get; } = a.DigitalCondensationCooling;
    public bool DigitalDropletSize { get; } = a.DigitalDropletSize;
    public bool DigitalOpticsTemperature { get; } = a.DigitalOpticsTemperature;
    public bool DigitalGlobalWarning { get; } = a.DigitalGlobalWarning;
    public bool DigitalGlobalError { get; } = a.DigitalGlobalError;
    public bool DigitalEvaporationHeating { get; } = a.DigitalEvaporationHeating;
}

