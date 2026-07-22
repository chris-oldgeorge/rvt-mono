using System.Text.Json.Serialization;

namespace MyAtm.Model.Json
{
    public class AccessoryInfo
    {
        [JsonRequired]
        [JsonPropertyName("saved_at")]
        public DateTime SavedAt { get; set; }

        [JsonRequired]
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_span_point_deviation")]
        public Double? OperatingSpanPointDeviation { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_t_led")]
        public Double? OperatingTLed { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_t_heating")]
        public Double? OperatingTHeating { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_volume_flow")]
        public Double? OperatingVolumeFlow { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_volume_flow_signal_length")]
        public Double? OperatingVolumeFlowSignalLength { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_volume_flow_timestamp")]
        public long OperatingVolumeFlowTimestamp { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_peak_position_15s")]
        public Double? OperatingPeakPosition15s { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_peak_quality_15s")]
        public Double? OperatingPeakQuality15s { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_velocity")]
        public Double? OperatingVelocity { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_sla_noise_level")]
        public Double? OperatingSlaNoiseLevel { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_sla_offset_adjustment_voltage")]
        public Double? OperatingSlaOffsetAdjustmentVoltage { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_t_mio")]
        public Double? OperatingTMio { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_p_mio")]
        public Double? OperatingPMio { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_rh_mio")]
        public Double? OperatingRHMio { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_auto_calibration_peak_position")]
        public Double? OperatingAutoCalibrationPeakPosition { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_power_led")]
        public Double? OperatingPowerLed { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_power_pmt")]
        public Double? OperatingPowerPmt { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_power_heating")]
        public Double? OperatingPowerHeating { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_power_volume_flow_blower")]
        public Double? OperatingPowerVolumeFlowBlower { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_power_housing_blower")]
        public Double? OperatingPowerHousingBlower { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_power_separator_blower")]
        public Double? OperatingPowerSeparatorBlower { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_flow_correction_factor")]
        public Double? OperatingFlowCorrectionFactor { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_t_gas_sensor_module")]
        public Double? OperatingTGasSensorModule { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_rh_gas_sensor_module")]
        public Double? OperatingRHGasSensorModule { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_coincidence")]
        public Double? OperatingCoincidence { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_modus")]
        public Double? OperatingModus { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_aerosol_distr_xu_0")]
        public Double? OperatingAerosolDistrXu0 { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_aerosol_distr_multiplier")]
        public Double? OperatingAerosolDistrMultiplier { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_gps_latitude")]
        public Double? OperatingGpsLatitude { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_gps_longitude")]
        public Double? OperatingGpsLongitude { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_so2_raw")]
        public Double? OperatingSO2Raw { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_no2_raw")]
        public Double? OperatingNO2Raw { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_o3_raw")]
        public Double? OperatingO3Raw { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_co_raw")]
        public Double? OperatingCORaw { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_t_nh3")]
        public Double? OperatingTNH3 { get; set; }

        [JsonRequired]
        [JsonPropertyName("operating_rh_nh3")]
        public Double? OperatingRHNH3 { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_calibration_enable_status")]
        public bool DigitalCalibrationEnableStatus { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_iads_connected")]
        public bool DigitalIadsConnected { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_iads_activated")]
        public bool DigitalIadsActivated { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_ambient_protection_attached")]
        public bool DigitalAmbientProtectionAttached { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_coincidence")]
        public bool DigitalCoincidence { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_weather_station")]
        public bool DigitalWeatherStation { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_operating_modus")]
        public bool DigitalOperatingModus { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_volume_flow")]
        public bool DigitalVolumeFlow { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_suction")]
        public bool DigitalSuction { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_iads")]
        public bool DigitalIads { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_calibration")]
        public bool DigitalCalibration { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_sensor_led")]
        public bool DigitalSensorLed { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_sensor_data")]
        public bool DigitalSensorData { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_sensor_noise")]
        public bool DigitalSensorNoise { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_count_modus")]
        public bool DigitalCountModus { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_liquid_pumps")]
        public bool DigitalLiquidPumps { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_condensation_cooling")]
        public bool DigitalCondensationCooling { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_droplet_size")]
        public bool DigitalDropletSize { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_optics_temperature")]
        public bool DigitalOpticsTemperature { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_global_warning")]
        public bool DigitalGlobalWarning { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_global_error")]
        public bool DigitalGlobalError { get; set; }

        [JsonRequired]
        [JsonPropertyName("digital_evaporation_heating")]
        public bool DigitalEvaporationHeating { get; set; }

        [JsonRequired]
        [JsonPropertyName("infos_sensor_status_so2")]
        public string? InfosSensorStatusSO2 { get; set; }

        [JsonRequired]
        [JsonPropertyName("infos_sensor_status_no2")]
        public string? InfosSensorStatusNO2 { get; set; }

        [JsonRequired]
        [JsonPropertyName("infos_sensor_status_o3")]
        public string? InfosSensorStatusO3 { get; set; }

        [JsonRequired]
        [JsonPropertyName("infos_sensor_status_co")]
        public string? InfosSensorStatusCO { get; set; }
    }
}

