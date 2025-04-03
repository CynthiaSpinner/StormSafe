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

    public class StormData
    {
        public StormData()
        {
            CurrentLocation = new Location();
            StormTypes = new List<string>();
            CurrentConditions = new CurrentConditions();
            Forecast = new List<ForecastPeriod>();
            PredictedPath = new List<Location>();
        }

        public Location CurrentLocation { get; set; }
        public List<string> StormTypes { get; set; }
        public CurrentConditions CurrentConditions { get; set; }
        public List<ForecastPeriod> Forecast { get; set; }
        public List<Location> PredictedPath { get; set; }
    }

    public class Location
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class CurrentConditions
    {
        public CurrentConditions()
        {
            Description = string.Empty;
        }

        public double Temperature { get; set; }
        public double WindSpeed { get; set; }
        public double WindDirection { get; set; }
        public double Precipitation { get; set; }
        public string Description { get; set; }
    }

    public class ForecastPeriod
    {
        public ForecastPeriod()
        {
            WindDirection = string.Empty;
            Description = string.Empty;
            DetailedForecast = string.Empty;
        }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int Temperature { get; set; }
        public double WindSpeed { get; set; }
        public string WindDirection { get; set; }
        public string Description { get; set; }
        public string DetailedForecast { get; set; }
    }

    public class RadarImageResponse
    {
        public RadarImageResponse()
        {
            Url = string.Empty;
        }

        public string Url { get; set; }
    }
}