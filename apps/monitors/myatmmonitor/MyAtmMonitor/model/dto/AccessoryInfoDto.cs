
using MyAtm.Model.Json;

namespace MyAtm.Model.Dto
{

    public class AccessoryInfoDto
    {
        public string SerialId { get; }
        public DateTime SampleTime { get; }
        public Double? OperatingSpanPointDeviation { get; }
        public Double? OperatingTLed { get; }
        public Double? OperatingTHeating { get; }
        public Double? OperatingVolumeFlow { get; }
        public Double? OperatingVolumeFlowSignalLength { get; }
        public long OperatingVolumeFlowTimestamp { get; }
        public Double? OperatingPeakPosition15s { get; }
        public Double? OperatingVelocity { get; }
        public Double? OperatingSlaNoiseLevel { get; }
        public Double? OperatingSlaOffsetAdjustmentVoltage { get; }
        public Double? OperatingTMio { get; }
        public Double? OperatingPMio { get; }
        public Double? OperatingRHMio { get; }
        public Double? OperatingAutoCalibrationPeakPosition { get; }
        public Double? OperatingPowerLed { get; }
        public Double? OperatingPowerPmt { get; }
        public Double? OperatingPowerHeating { get; }
        public Double? OperatingPowerVolumeFlowBlower { get; }
        public Double? OperatingPowerHousingBlower { get; }
        public Double? OperatingPowerSeparatorBlower { get; }
        public Double? OperatingFlowCorrectionFactor { get; }
        public bool DigitalCalibrationEnableStatus { get; }
        public bool DigitalIadsConnected { get; }
        public bool DigitalIadsActivated { get; }
        public bool DigitalAmbientProtectionAttached { get; }
        public bool DigitalCoincidence { get; }
        public bool DigitalWeatherStation { get; }
        public bool DigitalOperatingModus { get; }
        public bool DigitalVolumeFlow { get; }
        public bool DigitalSuction { get; }
        public bool DigitalIads { get; }
        public bool DigitalCalibration { get; }
        public bool DigitalSensorLed { get; }
        public bool DigitalSensorData { get; }
        public bool DigitalSensorNoise { get; }
        public bool DigitalCountModus { get; }
        public bool DigitalLiquidPumps { get; }
        public bool DigitalCondensationCooling { get; }
        public bool DigitalDropletSize { get; }
        public bool DigitalOpticsTemperature { get; }
        public bool DigitalGlobalWarning { get; }
        public bool DigitalGlobalError { get; }
        public bool DigitalEvaporationHeating { get; }


        public AccessoryInfoDto(string serialId, AccessoryInfo a)
        {
            SerialId = serialId;
            SampleTime = a.Timestamp;
            OperatingSpanPointDeviation = a.OperatingSpanPointDeviation;
            OperatingTLed = a.OperatingTLed;
            OperatingTHeating = a.OperatingTHeating;
            OperatingVolumeFlow = a.OperatingVolumeFlow;
            OperatingVolumeFlowSignalLength = a.OperatingVolumeFlowSignalLength;
            OperatingVolumeFlowTimestamp = a.OperatingVolumeFlowTimestamp;
            OperatingPeakPosition15s = a.OperatingPeakPosition15s;
            OperatingVelocity = a.OperatingVelocity;
            OperatingSlaNoiseLevel = a.OperatingSlaNoiseLevel;
            OperatingSlaOffsetAdjustmentVoltage = a.OperatingSlaOffsetAdjustmentVoltage;
            OperatingTMio = a.OperatingTMio;
            OperatingPMio = a.OperatingPMio;
            OperatingRHMio = a.OperatingRHMio;
            OperatingAutoCalibrationPeakPosition = a.OperatingAutoCalibrationPeakPosition;
            OperatingPowerLed = a.OperatingPowerLed;
            OperatingPowerPmt = a.OperatingPowerPmt;
            OperatingPowerHeating = a.OperatingPowerHeating;
            OperatingPowerVolumeFlowBlower = a.OperatingPowerVolumeFlowBlower;
            OperatingPowerHousingBlower = a.OperatingPowerHousingBlower;
            OperatingPowerSeparatorBlower = a.OperatingPowerSeparatorBlower;
            OperatingFlowCorrectionFactor = a.OperatingFlowCorrectionFactor;
            DigitalCalibrationEnableStatus = a.DigitalCalibrationEnableStatus;
            DigitalIadsConnected = a.DigitalIadsConnected;
            DigitalIadsActivated = a.DigitalIadsActivated;
            DigitalAmbientProtectionAttached = a.DigitalAmbientProtectionAttached;
            DigitalCoincidence = a.DigitalCoincidence;
            DigitalWeatherStation = a.DigitalWeatherStation;
            DigitalOperatingModus = a.DigitalOperatingModus;
            DigitalVolumeFlow = a.DigitalVolumeFlow;
            DigitalSuction = a.DigitalSuction;
            DigitalIads = a.DigitalIads;
            DigitalCalibration = a.DigitalCalibration;
            DigitalSensorLed = a.DigitalSensorLed;
            DigitalSensorData = a.DigitalSensorData;
            DigitalSensorNoise = a.DigitalSensorNoise;
            DigitalCountModus = a.DigitalCountModus;
            DigitalLiquidPumps = a.DigitalLiquidPumps;
            DigitalCondensationCooling = a.DigitalCondensationCooling;
            DigitalDropletSize = a.DigitalDropletSize;
            DigitalOpticsTemperature = a.DigitalOpticsTemperature;
            DigitalGlobalWarning = a.DigitalGlobalWarning;
            DigitalGlobalError = a.DigitalGlobalError;
            DigitalEvaporationHeating = a.DigitalEvaporationHeating;
        }
    }
}

