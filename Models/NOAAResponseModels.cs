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
        public List<ObservationStationFeature> Features { get; set; } = new();
    }

    public class ObservationStationFeature
    {
        [JsonPropertyName("properties")]
        public ObservationStationProperties Properties { get; set; } = new();
    }

    public class ObservationStationProperties
    {
        [JsonPropertyName("latestObservation")]
        public string? LatestObservation { get; set; }
    }

    public class GridPointResponse
    {
        [JsonPropertyName("properties")]
        public GridPointProperties Properties { get; set; } = new();
    }

    public class GridPointProperties
    {
        [JsonPropertyName("forecast")]
        public string? Forecast { get; set; }

        [JsonPropertyName("observationStations")]
        public string? ObservationStations { get; set; }

        [JsonPropertyName("alerts")]
        public string? Alerts { get; set; }
    }

    public class NOAAGeometry
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("coordinates")]
        public List<double>? Coordinates { get; set; }
    }

    public class NOAAConditions
    {
        [JsonPropertyName("@context")]
        public List<object>? Context { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("geometry")]
        public NOAAGeometry? Geometry { get; set; }

        [JsonPropertyName("properties")]
        public NOAAConditionsProperties? Properties { get; set; }
    }

    public class NOAAConditionsProperties
    {
        [JsonPropertyName("@id")]
        public string? Id { get; set; }

        [JsonPropertyName("@type")]
        public string? Type { get; set; }

        [JsonPropertyName("elevation")]
        public NOAAValue? Elevation { get; set; }

        [JsonPropertyName("station")]
        public string? Station { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [JsonPropertyName("rawMessage")]
        public string? RawMessage { get; set; }

        [JsonPropertyName("textDescription")]
        public string? TextDescription { get; set; }

        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        [JsonPropertyName("presentWeather")]
        public List<object>? PresentWeather { get; set; }

        [JsonPropertyName("temperature")]
        public NOAATemperature? Temperature { get; set; }

        [JsonPropertyName("dewpoint")]
        public NOAATemperature? Dewpoint { get; set; }

        [JsonPropertyName("windDirection")]
        public NOAATemperature? WindDirection { get; set; }

        [JsonPropertyName("windSpeed")]
        public NOAATemperature? WindSpeed { get; set; }

        [JsonPropertyName("windGust")]
        public NOAATemperature? WindGust { get; set; }

        [JsonPropertyName("barometricPressure")]
        public NOAATemperature? BarometricPressure { get; set; }

        [JsonPropertyName("seaLevelPressure")]
        public NOAATemperature? SeaLevelPressure { get; set; }

        [JsonPropertyName("visibility")]
        public NOAATemperature? Visibility { get; set; }

        [JsonPropertyName("maxTemperatureLast24Hours")]
        public NOAATemperature? MaxTemperatureLast24Hours { get; set; }

        [JsonPropertyName("minTemperatureLast24Hours")]
        public NOAATemperature? MinTemperatureLast24Hours { get; set; }

        [JsonPropertyName("precipitationLastHour")]
        public NOAATemperature? PrecipitationLastHour { get; set; }

        [JsonPropertyName("precipitationLast3Hours")]
        public NOAATemperature? PrecipitationLast3Hours { get; set; }

        [JsonPropertyName("precipitationLast6Hours")]
        public NOAATemperature? PrecipitationLast6Hours { get; set; }

        [JsonPropertyName("relativeHumidity")]
        public NOAATemperature? RelativeHumidity { get; set; }

        [JsonPropertyName("windChill")]
        public NOAATemperature? WindChill { get; set; }

        [JsonPropertyName("heatIndex")]
        public NOAATemperature? HeatIndex { get; set; }

        [JsonPropertyName("cloudLayers")]
        public List<NOAACloudLayer>? CloudLayers { get; set; }
    }

    public class NOAATemperature
    {
        [JsonPropertyName("unitCode")]
        public string? UnitCode { get; set; }

        [JsonPropertyName("value")]
        public double? Value { get; set; }

        [JsonPropertyName("qualityControl")]
        public string? QualityControl { get; set; }
    }

    public class NOAACloudLayer
    {
        [JsonPropertyName("base")]
        public NOAATemperature? Base { get; set; }

        [JsonPropertyName("amount")]
        public string? Amount { get; set; }
    }

    public class NOAADirectionValue
    {
        [JsonPropertyName("value")]
        public double? Value { get; set; }

        [JsonPropertyName("unitCode")]
        public string? UnitCode { get; set; }

        [JsonPropertyName("direction")]
        public string? Direction { get; set; }
    }

    public class NOAAValue
    {
        [JsonPropertyName("value")]
        public double? Value { get; set; }

        [JsonPropertyName("unitCode")]
        public string? UnitCode { get; set; }
    }

    public class NOAAForecastResponse
    {
        [JsonPropertyName("properties")]
        public NOAAForecastProperties Properties { get; set; } = new();
    }

    public class NOAAForecastProperties
    {
        [JsonPropertyName("periods")]
        public List<NOAAPeriod>? Periods { get; set; }
    }

    public class NOAAPeriod
    {
        [JsonPropertyName("startTime")]
        public string? StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public string? EndTime { get; set; }

        [JsonPropertyName("temperature")]
        public int Temperature { get; set; }

        [JsonPropertyName("windSpeed")]
        public string? WindSpeed { get; set; }

        [JsonPropertyName("windDirection")]
        public string? WindDirection { get; set; }

        [JsonPropertyName("shortForecast")]
        public string? ShortForecast { get; set; }

        [JsonPropertyName("detailedForecast")]
        public string? DetailedForecast { get; set; }
    }

    public class NOAAAlertsResponse
    {
        [JsonPropertyName("features")]
        public List<NOAAAlertFeature>? Features { get; set; }
    }

    public class NOAAAlertFeature
    {
        [JsonPropertyName("properties")]
        public NOAAAlertProperties? Properties { get; set; }
    }

    public class NOAAAlertProperties
    {
        [JsonPropertyName("event")]
        public string? Event { get; set; }

        [JsonPropertyName("severity")]
        public string? Severity { get; set; }

        [JsonPropertyName("urgency")]
        public string? Urgency { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("effective")]
        public string? Effective { get; set; }

        [JsonPropertyName("expires")]
        public string? Expires { get; set; }
    }

    public class NominatimLocation
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("importance")]
        public double Importance { get; set; }
    }
}