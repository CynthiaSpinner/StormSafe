using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StormSafe.Models;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace StormSafe.Services
{
    public interface IWeatherService
    {
        Task<StormData> GetStormDataAsync(double latitude, double longitude);
        string GetRadarImageUrl(double latitude, double longitude);
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
            _openWeatherApiKey = configuration["WeatherApi:OpenWeatherMapApiKey"]
                ?? throw new ArgumentNullException(nameof(configuration), "OpenWeatherMap API key is not configured");
            _logger = logger;
        }

        public async Task<StormData> GetStormDataAsync(double latitude, double longitude)
        {
            try
            {
                _logger.LogInformation($"Starting to fetch storm data for coordinates: lat={latitude}, lon={longitude}");

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

                // Convert OpenWeatherMap data to StormData
                var stormData = new StormData
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    Speed = weatherData.Wind?.Speed ?? 0,
                    Direction = weatherData.Wind?.Deg ?? 0,
                    Intensity = CalculateIntensity(weatherData),
                    EstimatedArrivalTime = CalculateEstimatedArrivalTime(forecastData, weatherData),
                    DistanceToUser = 0, // This would be calculated based on user's location
                    StormType = GetStormType(weatherData),
                    RadarImageUrl = GetRadarImageUrl(latitude, longitude),
                    PrecipitationRate = weatherData.Rain?.OneHour ?? 0,
                    WindSpeed = weatherData.Wind?.Speed ?? 0,
                    WindGust = weatherData.Wind?.Gust ?? 0,
                    AlertLevel = GetAlertLevel(weatherData),
                    StormDescription = weatherData.Weather[0]?.Description ?? "Unknown weather conditions",
                    HailSize = 0, // OpenWeatherMap doesn't provide hail data
                    HasLightning = weatherData.Weather[0]?.Main?.Contains("Thunderstorm") ?? false,
                    Visibility = weatherData.Visibility / 1609.34, // Convert meters to miles
                    PredictedPath = GeneratePredictedPath(forecastData, latitude, longitude)
                };

                _logger.LogInformation($"Successfully created storm data: {JsonSerializer.Serialize(stormData)}");
                return stormData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetStormDataAsync: {ex.Message}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");
                throw; // Re-throw the exception to be handled by the controller
            }
        }

        public string GetRadarImageUrl(double latitude, double longitude)
        {
            // Get the nearest radar station based on coordinates
            // For now, we'll use a static URL, but in a real application,
            // you would want to determine the nearest radar station
            return $"https://radar.weather.gov/ridge/standard/{GetRadarStation(latitude, longitude)}_loop.gif";
        }

        private string GetRadarStation(double latitude, double longitude)
        {
            // This is a simplified version. In a real application,
            // you would want to use a more sophisticated algorithm
            // to determine the nearest radar station based on coordinates
            if (latitude >= 40.0 && longitude <= -70.0)
                return "OKX"; // New York City area
            else if (latitude >= 35.0 && longitude <= -80.0)
                return "RAX"; // Raleigh area
            else if (latitude >= 30.0 && longitude <= -90.0)
                return "LIX"; // New Orleans area
            else
                return "DEFAULT"; // Default station
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
            foreach (var forecast in forecastData.List.Take(5))
            {
                if (forecast?.Weather == null || forecast.Weather.Length == 0)
                    continue;

                path.Add(new StormPathPoint
                {
                    Latitude = startLat + (path.Count * 0.1),
                    Longitude = startLng + (path.Count * 0.1),
                    Time = DateTime.Parse(forecast.DtTxt),
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
            foreach (var forecast in forecastData.List)
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
                    return DateTime.Parse(forecast.DtTxt);
                }
            }

            // If no significant weather is forecast, return a default time
            return DateTime.Now.AddHours(2);
        }

        private double CalculateIntensity(OpenWeatherForecastItem forecast)
        {
            var precipitationIntensity = forecast.Rain?.ThreeHour ?? 0;
            var windIntensity = forecast.Wind.Speed / 50.0;
            return Math.Min(100, (precipitationIntensity * 20 + windIntensity * 50));
        }

        private StormData GetMockData(double latitude, double longitude)
        {
            return new StormData
            {
                Speed = 25,
                Direction = 180,
                Intensity = 75,
                EstimatedArrivalTime = DateTime.Now.AddHours(2),
                DistanceToUser = 50,
                StormType = "Thunderstorm",
                RadarImageUrl = GetRadarImageUrl(latitude, longitude),
                PrecipitationRate = 1.5,
                WindSpeed = 35,
                WindGust = 45,
                AlertLevel = "Watch",
                StormDescription = "Strong thunderstorm with heavy rain and possible hail",
                HailSize = 0.5,
                HasLightning = true,
                Visibility = 2.5,
                PredictedPath = GenerateMockPath(latitude, longitude)
            };
        }

        private List<StormPathPoint> GenerateMockPath(double startLat, double startLng)
        {
            var path = new List<StormPathPoint>();
            var time = DateTime.Now;

            for (int i = 0; i < 5; i++)
            {
                path.Add(new StormPathPoint
                {
                    Latitude = startLat + (i * 0.1),
                    Longitude = startLng + (i * 0.1),
                    Time = time.AddMinutes(i * 30),
                    Intensity = 75 - (i * 5)
                });
            }

            return path;
        }
    }

    // OpenWeatherMap API response models
    public class OpenWeatherResponse
    {
        [JsonPropertyName("coord")]
        public Coordinates Coord { get; set; }

        [JsonPropertyName("weather")]
        public Weather[] Weather { get; set; }

        [JsonPropertyName("main")]
        public Main Main { get; set; }

        [JsonPropertyName("wind")]
        public Wind Wind { get; set; }

        [JsonPropertyName("rain")]
        public Rain Rain { get; set; }

        [JsonPropertyName("visibility")]
        public double Visibility { get; set; }
    }

    public class OpenWeatherForecastResponse
    {
        [JsonPropertyName("list")]
        public List<OpenWeatherForecastItem> List { get; set; }
    }

    public class OpenWeatherForecastItem
    {
        [JsonPropertyName("dt_txt")]
        public string DtTxt { get; set; }

        [JsonPropertyName("main")]
        public Main Main { get; set; }

        [JsonPropertyName("weather")]
        public Weather[] Weather { get; set; }

        [JsonPropertyName("wind")]
        public Wind Wind { get; set; }

        [JsonPropertyName("rain")]
        public Rain Rain { get; set; }
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
        public string Main { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }
    }

    public class Main
    {
        [JsonPropertyName("temp")]
        public double Temp { get; set; }

        [JsonPropertyName("humidity")]
        public double Humidity { get; set; }

        [JsonPropertyName("pressure")]
        public double Pressure { get; set; }
    }

    public class Wind
    {
        [JsonPropertyName("speed")]
        public double Speed { get; set; }

        [JsonPropertyName("deg")]
        public double Deg { get; set; }

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