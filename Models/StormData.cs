using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StormSafe.Models
{
    public class StormData
    {
        public required Location CurrentLocation { get; set; }
        public double Intensity { get; set; }
        public required List<string> StormTypes { get; set; }
        public required List<StormPathPoint> PredictedPath { get; set; }
        public required CurrentConditions CurrentConditions { get; set; }
        public required List<ForecastPeriod> Forecast { get; set; }
    }

    public class Location
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class CurrentConditions
    {
        public double Temperature { get; set; }
        public double WindSpeed { get; set; }
        public double WindDirection { get; set; }
        public double Precipitation { get; set; }
        public required string Description { get; set; }
    }

    public class ForecastPeriod
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int Temperature { get; set; }
        public double WindSpeed { get; set; }
        public required string WindDirection { get; set; }
        public required string Description { get; set; }
        public required string DetailedForecast { get; set; }
    }

    public class StormPathPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Time { get; set; }
        public double Intensity { get; set; }
    }
}