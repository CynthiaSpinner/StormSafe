using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StormSafe.Models;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace StormSafe.Services
{
    public interface IWeatherService
    {
        Task<StormData> GetStormDataAsync(double latitude, double longitude);
        Task<RadarImageResponse> GetRadarImageUrl(double latitude, double longitude);
    }

    public class WeatherService : IWeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly string _openWeatherApiKey;
        private readonly string _openWeatherBaseUrl = "https://api.openweathermap.org/data/2.5";
        private readonly ILogger<WeatherService> _logger;

        public WeatherService(HttpClient httpClient, IConfiguration configuration, ILogger<WeatherService> logger)
        {
            _httpClient = httpClient;
            _openWeatherApiKey = configuration["ApiKeys:OpenWeatherMap"]
                ?? throw new ArgumentNullException(nameof(configuration), "OpenWeatherMap API key is not configured");
            _logger = logger;

            // Add default headers for NOAA API
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "StormSafe/1.0 (https://github.com/yourusername/StormSafe)");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/geo+json");
        }

        public async Task<StormData> GetStormDataAsync(double latitude, double longitude)
        {
            try
            {
                _logger.LogInformation($"Starting to fetch weather data for coordinates: lat={latitude}, lon={longitude}");

                // Get current weather data
                var weatherUrl = $"{_openWeatherBaseUrl}/weather?lat={latitude}&lon={longitude}&appid={_openWeatherApiKey}&units=imperial";
                _logger.LogInformation($"Fetching weather data from: {weatherUrl}");

                var weatherResponse = await _httpClient.GetAsync(weatherUrl);
                var weatherJson = await weatherResponse.Content.ReadAsStringAsync();
                _logger.LogInformation($"Weather API Response: {weatherJson}");

                if (!weatherResponse.IsSuccessStatusCode)
                {
                    var errorMessage = $"Weather API returned status code: {weatherResponse.StatusCode}. Response: {weatherJson}";
                    _logger.LogError(errorMessage);
                    throw new Exception(errorMessage);
                }

                var weatherData = JsonSerializer.Deserialize<OpenWeatherResponse>(weatherJson);
                if (weatherData == null || weatherData.Weather == null || weatherData.Weather.Length == 0)
                {
                    var errorMessage = $"Invalid weather data received: {weatherJson}";
                    _logger.LogError(errorMessage);
                    throw new Exception(errorMessage);
                }

                // Get forecast data
                var forecastUrl = $"{_openWeatherBaseUrl}/forecast?lat={latitude}&lon={longitude}&appid={_openWeatherApiKey}&units=imperial";
                _logger.LogInformation($"Fetching forecast data from: {forecastUrl}");

                var forecastResponse = await _httpClient.GetAsync(forecastUrl);
                var forecastJson = await forecastResponse.Content.ReadAsStringAsync();
                _logger.LogInformation($"Forecast API Response: {forecastJson}");

                if (!forecastResponse.IsSuccessStatusCode)
                {
                    var errorMessage = $"Forecast API returned status code: {forecastResponse.StatusCode}. Response: {forecastJson}";
                    _logger.LogError(errorMessage);
                    throw new Exception(errorMessage);
                }

                var forecastData = JsonSerializer.Deserialize<OpenWeatherForecastResponse>(forecastJson);
                if (forecastData == null || forecastData.List == null || forecastData.List.Count == 0)
                {
                    var errorMessage = $"Invalid forecast data received: {forecastJson}";
                    _logger.LogError(errorMessage);
                    throw new Exception(errorMessage);
                }

                // Determine if there's a storm based on weather conditions
                bool hasStorm = IsStormPresent(weatherData, forecastData);

                // Convert OpenWeatherMap data to StormData
                var stormData = new StormData
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    Speed = weatherData.Wind?.Speed ?? 0,
                    Direction = weatherData.Wind?.Deg ?? 0,
                    Intensity = CalculateIntensity(weatherData),
                    EstimatedArrivalTime = hasStorm ? CalculateEstimatedArrivalTime(forecastData, weatherData) : DateTime.UtcNow.AddHours(1),
                    DistanceToUser = 0,
                    StormType = hasStorm ? GetStormType(weatherData) : "Clear Weather",
                    RadarImageUrl = (await GetRadarImageUrl(latitude, longitude)).Url,
                    PrecipitationRate = weatherData.Rain?.OneHour ?? 0,
                    WindSpeed = weatherData.Wind?.Speed ?? 0,
                    WindGust = weatherData.Wind?.Gust ?? 0,
                    AlertLevel = hasStorm ? GetAlertLevel(weatherData) : "None",
                    StormDescription = weatherData.Weather[0]?.Description ?? "Unknown weather conditions",
                    HailSize = 0,
                    HasLightning = weatherData.Weather[0]?.Main?.Contains("Thunderstorm") ?? false,
                    Visibility = weatherData.Visibility / 1609.34, // Convert meters to miles
                    PredictedPath = hasStorm ? GeneratePredictedPath(forecastData, latitude, longitude) : new List<StormPathPoint>()
                };

                _logger.LogInformation($"Successfully created weather data: {JsonSerializer.Serialize(stormData)}");

                var forecastItems = forecastData.List ?? new List<OpenWeatherForecastItem>();
                var dailyForecasts = forecastItems
                    .GroupBy(f => DateTime.Parse(f.DtTxt ?? DateTime.Now.ToString()))
                    .Select(g => new DailyForecast
                    {
                        Date = g.Key,
                        HighTemp = g.Max(f => f.Main?.Temp ?? 0),
                        LowTemp = g.Min(f => f.Main?.Temp ?? 0),
                        Description = g.First().Weather?[0]?.Description ?? "Unknown",
                        Icon = g.First().Weather?[0]?.Icon ?? "01d"
                    })
                    .Take(5)
                    .ToList();

                var hourlyForecasts = forecastItems
                    .Where(f => DateTime.Parse(f.DtTxt ?? DateTime.Now.ToString()) <= DateTime.Now.AddHours(24))
                    .Select(f => new HourlyForecast
                    {
                        Time = DateTime.Parse(f.DtTxt ?? DateTime.Now.ToString()),
                        Temp = f.Main?.Temp ?? 0,
                        Description = f.Weather?[0]?.Description ?? "Unknown",
                        Icon = f.Weather?[0]?.Icon ?? "01d"
                    })
                    .ToList();

                stormData.DailyForecasts = dailyForecasts;
                stormData.HourlyForecasts = hourlyForecasts;

                return stormData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetStormDataAsync: {ex.Message}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<RadarImageResponse> GetRadarImageUrl(double latitude, double longitude)
        {
            try
            {
                // Try to get the radar station from NOAA API
                string radarStation;
                try
                {
                    radarStation = await GetBestRadarStation(latitude, longitude);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get radar station from NOAA API, using fallback station");
                    // Fallback to a known radar station based on coordinates
                    if (latitude >= 40.0 && longitude <= -70.0)
                        radarStation = "KOKX"; // New York
                    else if (latitude >= 35.0 && longitude <= -80.0)
                        radarStation = "KRAX"; // Raleigh
                    else if (latitude >= 30.0 && longitude <= -90.0)
                        radarStation = "KLIX"; // New Orleans
                    else
                        radarStation = "KOKX"; // Default to New York
                }

                // Construct the NEXRAD URL with the correct format
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                // Use the Iowa State Mesonet NEXRAD tile service
                var url = $"https://mesonet.agron.iastate.edu/cache/tile.py/1.0.0/nexrad-N0R-{radarStation}/{timestamp}/{{z}}/{{x}}/{{y}}.png";

                // Verify the URL is valid
                try
                {
                    var testUrl = url.Replace("{z}", "10").Replace("{x}", "264").Replace("{y}", "420");
                    var response = await _httpClient.GetAsync(testUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning($"Radar tile URL test failed: {response.StatusCode}");
                        // Fallback to a different radar station if the first one fails
                        radarStation = "KOKX"; // Default to New York
                        url = $"https://mesonet.agron.iastate.edu/cache/tile.py/1.0.0/nexrad-N0R-{radarStation}/{timestamp}/{{z}}/{{x}}/{{y}}.png";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to verify radar tile URL");
                }

                _logger.LogInformation($"Generated radar URL for station {radarStation}: {url}");
                return new RadarImageResponse { Url = url };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting radar image URL");
                // Return an empty URL instead of throwing an exception
                return new RadarImageResponse { Url = string.Empty };
            }
        }

        private async Task<string> GetBestRadarStation(double latitude, double longitude)
        {
            try
            {
                // Get all radar stations
                var client = new HttpClient();
                var response = await client.GetAsync("https://api.weather.gov/radar/stations");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var stations = JsonSerializer.Deserialize<RadarStationsResponse>(content);

                if (stations?.Features == null)
                {
                    _logger.LogError("Failed to deserialize radar stations response or Features is null");
                    throw new InvalidOperationException("Failed to get radar stations data");
                }

                // Filter for operational stations
                var operationalStations = stations.Features
                    .Where(s => s.Properties.Rda?.Properties?.OperabilityStatus == "RDA - On-line")
                    .Where(s => s.Properties.Rda?.Properties?.AlarmSummary == "No Alarms")
                    .Where(s => s.Properties.Rda?.Properties?.Mode == "Operational")
                    .ToList();

                // Find the closest operational station
                var closestStation = operationalStations
                    .OrderBy(s => CalculateDistance(latitude, longitude,
                        s.Geometry.Coordinates[1], s.Geometry.Coordinates[0]))
                    .FirstOrDefault();

                return closestStation?.Properties.Id ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting best radar station");
                return string.Empty;
            }
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Earth's radius in kilometers
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        private double CalculateIntensity(OpenWeatherResponse weatherData)
        {
            // Calculate intensity based on precipitation and wind
            var precipitationIntensity = weatherData.Rain?.OneHour ?? 0;
            var windIntensity = weatherData.Wind?.Speed / 50.0 ?? 0; // Normalize wind speed to 0-1 range

            return Math.Min(100, (precipitationIntensity * 20 + windIntensity * 50));
        }

        private string GetStormType(OpenWeatherResponse weatherData)
        {
            if (weatherData?.Weather == null || weatherData.Weather.Length == 0)
                return "Unknown";

            var mainWeather = weatherData.Weather[0]?.Main?.ToLower() ?? "unknown";
            return mainWeather switch
            {
                "thunderstorm" => "Thunderstorm",
                "rain" => "Rain Storm",
                "snow" => "Snow Storm",
                "drizzle" => "Light Rain",
                _ => "Weather System"
            };
        }

        private string GetAlertLevel(OpenWeatherResponse weatherData)
        {
            var windSpeed = weatherData.Wind?.Speed ?? 0;
            var precipitation = weatherData.Rain?.OneHour ?? 0;

            if (windSpeed > 50 || precipitation > 2)
                return "Warning";
            if (windSpeed > 30 || precipitation > 1)
                return "Watch";
            return "Advisory";
        }

        private List<StormPathPoint> GeneratePredictedPath(OpenWeatherForecastResponse forecastData, double startLat, double startLng)
        {
            var path = new List<StormPathPoint>();
            var time = DateTime.Now;

            // Use forecast data to generate path points
            var forecastList = forecastData.List ?? new List<OpenWeatherForecastItem>();
            foreach (var forecast in forecastList.Take(5))
            {
                if (forecast?.Weather == null || forecast.Weather.Length == 0)
                    continue;

                path.Add(new StormPathPoint
                {
                    Latitude = startLat + (path.Count * 0.1),
                    Longitude = startLng + (path.Count * 0.1),
                    Time = DateTime.Parse(forecast.DtTxt ?? DateTime.Now.ToString()),
                    Intensity = CalculateIntensity(forecast)
                });
            }

            return path;
        }

        private DateTime CalculateEstimatedArrivalTime(OpenWeatherForecastResponse forecastData, OpenWeatherResponse currentWeather)
        {
            if (forecastData?.List == null || currentWeather?.Weather == null || currentWeather.Weather.Length == 0)
                return DateTime.Now.AddHours(2);

            // Get the current weather conditions
            var currentIntensity = CalculateIntensity(currentWeather);

            // Find the first forecast point where the weather conditions are significant
            var forecastList = forecastData.List ?? new List<OpenWeatherForecastItem>();
            foreach (var forecast in forecastList)
            {
                if (forecast?.Weather == null || forecast.Weather.Length == 0)
                    continue;

                var forecastIntensity = CalculateIntensity(forecast);

                // If the forecast shows significant weather (intensity > 30)
                // or if there's a change in weather type (e.g., from clear to rain)
                if (forecastIntensity > 30 ||
                    (forecast.Weather[0]?.Main != currentWeather.Weather[0]?.Main &&
                     (forecast.Weather[0]?.Main == "Rain" || forecast.Weather[0]?.Main == "Thunderstorm")))
                {
                    return DateTime.Parse(forecast.DtTxt ?? DateTime.Now.ToString());
                }
            }

            // If no significant weather is forecast, return a default time
            return DateTime.Now.AddHours(2);
        }

        private double CalculateIntensity(OpenWeatherForecastItem forecast)
        {
            var precipitationIntensity = forecast.Rain?.ThreeHour ?? 0;
            var windIntensity = (forecast.Wind?.Speed ?? 0) / 50.0;
            return Math.Min(100, (precipitationIntensity * 20 + windIntensity * 50));
        }

        private bool IsStormPresent(OpenWeatherResponse weatherData, OpenWeatherForecastResponse forecastData)
        {
            // Check current weather for storm conditions
            if (weatherData.Weather != null)
            {
                foreach (var weather in weatherData.Weather)
                {
                    if (weather.Main?.Contains("Thunderstorm") == true ||
                        weather.Main?.Contains("Rain") == true ||
                        weather.Main?.Contains("Snow") == true)
                    {
                        return true;
                    }
                }
            }

            // Check forecast for upcoming storm conditions
            if (forecastData.List != null)
            {
                foreach (var forecast in forecastData.List)
                {
                    if (forecast.Weather != null)
                    {
                        foreach (var weather in forecast.Weather)
                        {
                            if (weather.Main?.Contains("Thunderstorm") == true ||
                                weather.Main?.Contains("Rain") == true ||
                                weather.Main?.Contains("Snow") == true)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private class PointsResponse
        {
            public Properties Properties { get; set; } = new();
        }

        private class Properties
        {
            public string RadarStation { get; set; } = string.Empty;
        }
    }

    // OpenWeatherMap API response models
    public class OpenWeatherResponse
    {
        [JsonPropertyName("coord")]
        public Coordinates? Coord { get; set; }

        [JsonPropertyName("weather")]
        public Weather[]? Weather { get; set; }

        [JsonPropertyName("main")]
        public Main? Main { get; set; }

        [JsonPropertyName("wind")]
        public Wind? Wind { get; set; }

        [JsonPropertyName("rain")]
        public Rain? Rain { get; set; }

        [JsonPropertyName("visibility")]
        public int Visibility { get; set; }
    }

    public class OpenWeatherForecastResponse
    {
        [JsonPropertyName("list")]
        public List<OpenWeatherForecastItem>? List { get; set; }
    }

    public class OpenWeatherForecastItem
    {
        [JsonPropertyName("dt_txt")]
        public string? DtTxt { get; set; }

        [JsonPropertyName("main")]
        public Main? Main { get; set; }

        [JsonPropertyName("weather")]
        public Weather[]? Weather { get; set; }

        [JsonPropertyName("wind")]
        public Wind? Wind { get; set; }

        [JsonPropertyName("rain")]
        public Rain? Rain { get; set; }
    }

    public class Coordinates
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }
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

    public class Main
    {
        [JsonPropertyName("temp")]
        public double Temp { get; set; }

        [JsonPropertyName("feels_like")]
        public double FeelsLike { get; set; }

        [JsonPropertyName("pressure")]
        public int Pressure { get; set; }

        [JsonPropertyName("humidity")]
        public int Humidity { get; set; }
    }

    public class Wind
    {
        [JsonPropertyName("speed")]
        public double Speed { get; set; }

        [JsonPropertyName("deg")]
        public int Deg { get; set; }

        [JsonPropertyName("gust")]
        public double Gust { get; set; }
    }

    public class Rain
    {
        [JsonPropertyName("1h")]
        public double OneHour { get; set; }

        [JsonPropertyName("3h")]
        public double ThreeHour { get; set; }
    }
}