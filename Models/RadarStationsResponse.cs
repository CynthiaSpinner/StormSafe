using System.Text.Json.Serialization;

namespace StormSafe.Models
{
    public class RadarStationsResponse
    {
        [JsonPropertyName("features")]
        public required List<RadarStationFeature> Features { get; set; }

        public RadarStationsResponse()
        {
            Features = new List<RadarStationFeature>();
        }
    }

    public class RadarStationFeature
    {
        [JsonPropertyName("geometry")]
        public required Geometry Geometry { get; set; }

        [JsonPropertyName("properties")]
        public required RadarStationProperties Properties { get; set; }

        public RadarStationFeature()
        {
            Geometry = new Geometry { Coordinates = new List<double>() };
            Properties = new RadarStationProperties
            {
                Id = string.Empty,
                Rda = new RadarDataAttributes
                {
                    Properties = new RadarProperties
                    {
                        OperabilityStatus = string.Empty,
                        AlarmSummary = string.Empty,
                        Mode = string.Empty
                    }
                }
            };
        }
    }

    public class Geometry
    {
        [JsonPropertyName("coordinates")]
        public required List<double> Coordinates { get; set; }

        public Geometry()
        {
            Coordinates = new List<double>();
        }
    }

    public class RadarStationProperties
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("rda")]
        public required RadarDataAttributes Rda { get; set; }

        public RadarStationProperties()
        {
            Id = string.Empty;
            Rda = new RadarDataAttributes
            {
                Properties = new RadarProperties
                {
                    OperabilityStatus = string.Empty,
                    AlarmSummary = string.Empty,
                    Mode = string.Empty
                }
            };
        }
    }

    public class RadarDataAttributes
    {
        [JsonPropertyName("properties")]
        public required RadarProperties Properties { get; set; }

        public RadarDataAttributes()
        {
            Properties = new RadarProperties
            {
                OperabilityStatus = string.Empty,
                AlarmSummary = string.Empty,
                Mode = string.Empty
            };
        }
    }

    public class RadarProperties
    {
        [JsonPropertyName("operabilityStatus")]
        public required string OperabilityStatus { get; set; }

        [JsonPropertyName("alarmSummary")]
        public required string AlarmSummary { get; set; }

        [JsonPropertyName("mode")]
        public required string Mode { get; set; }

        public RadarProperties()
        {
            OperabilityStatus = string.Empty;
            AlarmSummary = string.Empty;
            Mode = string.Empty;
        }
    }
}