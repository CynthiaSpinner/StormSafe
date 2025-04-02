using System.Text.Json.Serialization;

namespace StormSafe.Models
{
    public class DailyForecast
    {
        public DateTime Date { get; set; }
        public double HighTemp { get; set; }
        public double LowTemp { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }

    public class HourlyForecast
    {
        public DateTime Time { get; set; }
        public double Temp { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }

    public class Weather
    {
        [JsonPropertyName("main")]
        public string? Main { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("icon")]
        public string? Icon { get; set; }
    }
}