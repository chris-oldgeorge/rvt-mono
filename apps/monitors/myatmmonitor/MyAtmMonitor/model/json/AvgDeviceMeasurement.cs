using System.Text.Json.Serialization;

namespace MyAtm.Model.Json
{

    public class AvgVal
    {

        [JsonRequired]
        [JsonPropertyName("avg")]
        public Double? Avg { get; set; }

        // hourly and daily have min and max fields for AvgVal
    }

    public class AvgDeviceMeasurement : BaseDeviceMeasurement
    {
        [JsonRequired]
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm0_2")]
        public AvgVal? Pm0_2 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm0_3")]
        public AvgVal? Pm0_3 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm0_4")]
        public AvgVal? Pm0_4 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm0_5")]
        public AvgVal? Pm0_5 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm1")]
        public AvgVal? Pm1 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm2_5")]
        public AvgVal? Pm2_5 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm4")]
        public AvgVal? Pm4 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm7")]
        public AvgVal? Pm7 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm10")]
        public AvgVal? Pm10 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm15")]
        public AvgVal? Pm15 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm20")]
        public AvgVal? Pm20 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm_total")]
        public AvgVal? PmTotal { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm_reserve_1")]
        public AvgVal? Pm_reserve_1 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm_reserve_2")]
        public AvgVal? Pm_reserve_2 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm_reserve_3")]
        public AvgVal? Pm_reserve_3 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm_reserve_4")]
        public AvgVal? Pm_reserve_4 { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm2_5_ce")]
        public AvgVal? Pm2_5_ce { get; set; }

        [JsonRequired]
        [JsonPropertyName("pm10_ce")]
        public AvgVal? Pm10_ce { get; set; }

        [JsonRequired]
        [JsonPropertyName("gas_co2")]
        public AvgVal? Gas_co2 { get; set; }

        [JsonRequired]
        [JsonPropertyName("gas_voc")]
        public AvgVal? Gas_voc { get; set; }

        [JsonRequired]
        [JsonPropertyName("gas_so2")]
        public AvgVal? Gas_so2 { get; set; }

        [JsonRequired]
        [JsonPropertyName("gas_no2")]
        public AvgVal? Gas_no2 { get; set; }

        [JsonRequired]
        [JsonPropertyName("gas_o3")]
        public AvgVal? Gas_o3 { get; set; }

        [JsonRequired]
        [JsonPropertyName("gas_co")]
        public AvgVal? Gas_co { get; set; }

        [JsonRequired]
        [JsonPropertyName("gas_nh3")]
        public AvgVal? Gas_nh3 { get; set; }

        [JsonRequired]
        [JsonPropertyName("aerosol_cn")]
        public AvgVal? Aerosol_cn { get; set; }

        [JsonRequired]
        [JsonPropertyName("aerosol_m1_0")]
        public AvgVal? Aerosol_m1_0 { get; set; }

        [JsonRequired]
        [JsonPropertyName("aerosol_m2_0")]
        public AvgVal? Aerosol_m2_0 { get; set; }

        [JsonRequired]
        [JsonPropertyName("aerosol_m3_0")]
        public AvgVal? Aerosol_m3_0 { get; set; }

        [JsonRequired]
        [JsonPropertyName("aerosol_x10dcn")]
        public AvgVal? Aerosol_x10dcn { get; set; }

        [JsonRequired]
        [JsonPropertyName("aerosol_x16dcn")]
        public AvgVal? Aerosol_x16dcn { get; set; }

        [JsonRequired]
        [JsonPropertyName("aerosol_x50dcn")]
        public AvgVal? Aerosol_x50dcn { get; set; }

        [JsonRequired]
        [JsonPropertyName("aerosol_x84dcn")]
        public AvgVal? Aerosol_x84dcn { get; set; }

        [JsonRequired]
        [JsonPropertyName("aerosol_x90dcn")]
        public AvgVal? Aerosol_x90dcn { get; set; }

        [JsonRequired]
        [JsonPropertyName("aerosol_ldsa")]
        public AvgVal? Aerosol_ldsa { get; set; }

        [JsonRequired]
        [JsonPropertyName("indexes_aqi")]
        public AvgVal? Indexes_aqi { get; set; }

        [JsonRequired]
        [JsonPropertyName("indexes_infection")]
        public AvgVal? Indexes_infection { get; set; }

        [JsonRequired]
        [JsonPropertyName("weather_t")]
        public AvgVal? Weather_t { get; set; }

        [JsonRequired]
        [JsonPropertyName("weather_p")]
        public AvgVal? Weather_p { get; set; }

        [JsonRequired]
        [JsonPropertyName("weather_rh")]
        public AvgVal? Weather_rh { get; set; }

        [JsonRequired]
        [JsonPropertyName("weather_wind_speed")]
        public AvgVal? Weather_wind_speed { get; set; }

        [JsonRequired]
        [JsonPropertyName("weather_wind_direction")]
        public AvgVal? Weather_wind_direction { get; set; }

        [JsonRequired]
        [JsonPropertyName("weather_precipitation_intensity")]
        public AvgVal? Weather_precipitation_intensity { get; set; }

        [JsonRequired]
        [JsonPropertyName("weather_precipitation_type")]
        public AvgVal? Weather_precipitation_type { get; set; }

        [JsonRequired]
        [JsonPropertyName("weather_temperature_dew_point")]
        public AvgVal? Weather_temperature_dew_point { get; set; }

        [JsonRequired]
        [JsonPropertyName("weather_wind_signal_quality")]
        public AvgVal? Weather_wind_signal_quality { get; set; }

        [JsonRequired]
        [JsonPropertyName("weather_radiation")]
        public AvgVal? Weather_radiation { get; set; }

        [JsonRequired]
        [JsonPropertyName("weather_lightning_detection")]
        public AvgVal? Weather_lightning_detection { get; set; }
    }
}
