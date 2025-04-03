using System.Text.Json.Serialization;

namespace StormSafe.Models
{
    public class Station
    {
        [JsonPropertyName("properties")]
        public required StationProperties Properties { get; set; }

        [JsonPropertyName("geometry")]
        public required StationGeometry Geometry { get; set; }
    }

    public class StationProperties
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("stationIdentifier")]
        public required string StationIdentifier { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }

    public class StationGeometry
    {
        [JsonPropertyName("coordinates")]
        public required double[] Coordinates { get; set; }
    }
}