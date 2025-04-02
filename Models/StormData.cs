using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StormSafe.Models
{
    public class StormData
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Speed { get; set; } // Speed in mph
        public double Direction { get; set; } // Direction in degrees
        public double Intensity { get; set; } // Storm intensity (0-100)
        public DateTime EstimatedArrivalTime { get; set; }
        public double DistanceToUser { get; set; } // Distance in miles
        public string? StormType { get; set; }
        public string? RadarImageUrl { get; set; }

        // New fields
        public double PrecipitationRate { get; set; } // Inches per hour
        public double WindSpeed { get; set; } // Wind speed in mph
        public double WindGust { get; set; } // Wind gusts in mph
        public string? AlertLevel { get; set; } // Watch, Warning, etc.
        public string? StormDescription { get; set; }
        public List<StormPathPoint> PredictedPath { get; set; } = new();
        public double HailSize { get; set; } // Hail size in inches
        public bool HasLightning { get; set; }
        public double Visibility { get; set; } // Visibility in miles
        public List<DailyForecast> DailyForecasts { get; set; } = new();
        public List<HourlyForecast> HourlyForecasts { get; set; } = new();
    }

    public class StormPathPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Time { get; set; }
        public double Intensity { get; set; }
    }
}