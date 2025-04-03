using System.Text.Json.Serialization;

namespace StormSafe.Models
{
    public class RadarStationsResponse
    {
        [JsonPropertyName("features")]
        public List<RadarStationFeature>? Features { get; set; }
    }

    public class RadarStationFeature
    {
        [JsonPropertyName("properties")]
        public RadarStationProperties? Properties { get; set; }

        [JsonPropertyName("geometry")]
        public RadarStationGeometry? Geometry { get; set; }
    }

    public class RadarStationProperties
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("rda")]
        public RadarStationRda? Rda { get; set; }
    }

    public class RadarStationRda
    {
        [JsonPropertyName("properties")]
        public RadarStationRdaProperties? Properties { get; set; }
    }

    public class RadarStationRdaProperties
    {
        [JsonPropertyName("operabilityStatus")]
        public string? OperabilityStatus { get; set; }

        [JsonPropertyName("alarmSummary")]
        public string? AlarmSummary { get; set; }

        [JsonPropertyName("mode")]
        public string? Mode { get; set; }
    }

    public class RadarStationGeometry
    {
        [JsonPropertyName("coordinates")]
        public List<double>? Coordinates { get; set; }
    }

    public class ObservationStationsResponse
    {
        [JsonPropertyName("features")]
        public List<ObservationStationFeature>? Features { get; set; }
    }

    public class ObservationStationFeature
    {
        [JsonPropertyName("properties")]
        public ObservationStationProperties? Properties { get; set; }
    }

    public class ObservationStationProperties
    {
        [JsonPropertyName("stationIdentifier")]
        public string? StationIdentifier { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("latestObservation")]
        public string? LatestObservation { get; set; }
    }

    public class GridPointResponse
    {
        [JsonPropertyName("properties")]
        public GridPointProperties? Properties { get; set; }
    }

    public class GridPointProperties
    {
        [JsonPropertyName("gridId")]
        public string? GridId { get; set; }

        [JsonPropertyName("gridX")]
        public int GridX { get; set; }

        [JsonPropertyName("gridY")]
        public int GridY { get; set; }

        [JsonPropertyName("forecast")]
        public string? Forecast { get; set; }

        [JsonPropertyName("observationStations")]
        public string? ObservationStations { get; set; }
    }

    public class NOAAConditions
    {
        [JsonPropertyName("properties")]
        public NOAAConditionsProperties? Properties { get; set; }
    }

    public class NOAAConditionsProperties
    {
        [JsonPropertyName("textDescription")]
        public string? TextDescription { get; set; }

        [JsonPropertyName("temperature")]
        public NOAAValue? Temperature { get; set; }

        [JsonPropertyName("windSpeed")]
        public NOAAValue? WindSpeed { get; set; }

        [JsonPropertyName("windDirection")]
        public NOAAValue? WindDirection { get; set; }

        [JsonPropertyName("barometricPressure")]
        public NOAAValue? BarometricPressure { get; set; }

        [JsonPropertyName("precipitation")]
        public NOAAValue? Precipitation { get; set; }

        [JsonPropertyName("relativeHumidity")]
        public NOAAValue? RelativeHumidity { get; set; }

        [JsonPropertyName("visibility")]
        public NOAAValue? Visibility { get; set; }
    }

    public class NOAAValue
    {
        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("unitCode")]
        public string? UnitCode { get; set; }
    }

    public class NOAAForecastResponse
    {
        [JsonPropertyName("properties")]
        public NOAAForecastProperties? Properties { get; set; }
    }

    public class NOAAForecastProperties
    {
        [JsonPropertyName("periods")]
        public List<NOAAPeriod>? Periods { get; set; }
    }

    public class NOAAPeriod
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("startTime")]
        public string? StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public string? EndTime { get; set; }

        [JsonPropertyName("isDaytime")]
        public bool IsDaytime { get; set; }

        [JsonPropertyName("temperature")]
        public int Temperature { get; set; }

        [JsonPropertyName("temperatureUnit")]
        public string? TemperatureUnit { get; set; }

        [JsonPropertyName("windSpeed")]
        public string? WindSpeed { get; set; }

        [JsonPropertyName("windDirection")]
        public string? WindDirection { get; set; }

        [JsonPropertyName("shortForecast")]
        public string? ShortForecast { get; set; }

        [JsonPropertyName("detailedForecast")]
        public string? DetailedForecast { get; set; }
    }
}