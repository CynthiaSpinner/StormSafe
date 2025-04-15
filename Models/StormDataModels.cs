using System;

namespace StormSafe.Models
{
    public class StormData
    {
        public double Temperature { get; set; }
        public double WindSpeed { get; set; }
        public string StationId { get; set; }
        public string StationName { get; set; }
        public double Distance { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime LastUpdated { get; set; }
        public StormConditions StormConditions { get; set; }
        public LocalWeatherConditions LocalWeather { get; set; }
        public List<ForecastPeriod> ForecastPeriods { get; set; }
        public List<WeatherAlert> Alerts { get; set; }
    }

    public class StormConditions
    {
        public string StormType { get; set; }
        public int StormIntensity { get; set; }
        public string StormDescription { get; set; }
        public double WindGust { get; set; }
        public double Precipitation { get; set; }
        public double Visibility { get; set; }
        public double CloudCover { get; set; }
    }

    public class LocalWeatherConditions
    {
        public double Temperature { get; set; }
        public double WindSpeed { get; set; }
        public double WindDirection { get; set; }
        public double Dewpoint { get; set; }
        public double WindChill { get; set; }
        public double HeatIndex { get; set; }
        public double RelativeHumidity { get; set; }
        public double BarometricPressure { get; set; }
        public double SeaLevelPressure { get; set; }
        public string StationName { get; set; }
        public double StationDistance { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ForecastPeriod
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string ShortForecast { get; set; }
        public string DetailedForecast { get; set; }
        public double Temperature { get; set; }
        public string WindDirection { get; set; }
        public string WindSpeed { get; set; }
    }

    public class WeatherAlert
    {
        public string Event { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Description { get; set; }
        public string Severity { get; set; }
    }
}