using System;
using System.Collections.Generic;

namespace StormSafe.Models
{
    public class StormData
    {
        // Basic observation data
        public double Temperature { get; set; }
        public double WindSpeed { get; set; }
        public string StationId { get; set; } = string.Empty;
        public string StationName { get; set; } = string.Empty;
        public double Distance { get; set; }
        public DateTime Timestamp { get; set; }

        // Extended data
        public CurrentConditions? CurrentConditions { get; set; }
        public List<ForecastPeriod> ForecastPeriods { get; set; } = new();
        public List<WeatherAlert> Alerts { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    public class CurrentConditions
    {
        public double Temperature { get; set; }
        public double WindSpeed { get; set; }
        public string WindDirection { get; set; } = "N";
        public double Precipitation { get; set; }
        public double Pressure { get; set; }
        public double Visibility { get; set; }
        public int CloudCover { get; set; }
        public double Humidity { get; set; }
        public string StormType { get; set; } = "None";
        public int StormIntensity { get; set; }
        public string StormDescription { get; set; } = "No storm activity";
        public string StationName { get; set; } = "Unknown";
        public double StationDistance { get; set; }
    }

    public class ForecastPeriod
    {
        public string Name { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public double WindSpeed { get; set; }
        public double Precipitation { get; set; }
        public int StormProbability { get; set; }
        public string StormType { get; set; } = "None";
        public int StormIntensity { get; set; }
    }

    public class WeatherAlert
    {
        public string Event { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Urgency { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}