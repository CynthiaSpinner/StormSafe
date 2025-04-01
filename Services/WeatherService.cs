using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using StormSafe.Models;

namespace StormSafe.Services
{
    public interface IWeatherService
    {
        Task<StormData> GetStormDataAsync(double userLatitude, double userLongitude);
        Task<string> GetRadarImageUrlAsync(double latitude, double longitude);
    }

    public class WeatherService : IWeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly string _openWeatherApiKey;
        private readonly string _nwsApiBaseUrl = "https://api.weather.gov";

        public WeatherService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _openWeatherApiKey = configuration["OpenWeatherApi:ApiKey"];
        }

        public async Task<StormData> GetStormDataAsync(double userLatitude, double userLongitude)
        {
            // Get weather alerts from NWS API
            var response = await _httpClient.GetAsync($"{_nwsApiBaseUrl}/alerts/active?point={userLatitude},{userLongitude}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var alerts = JsonSerializer.Deserialize<JsonElement>(content);

            // Process the alerts and create StormData object
            var stormData = new StormData
            {
                Latitude = userLatitude,
                Longitude = userLongitude,
                // Additional processing would be needed here to extract specific storm data
                // This is a simplified example
                Speed = 30, // Example speed
                Direction = 45, // Example direction
                Intensity = 75, // Example intensity
                EstimatedArrivalTime = DateTime.Now.AddHours(2), // Example time
                DistanceToUser = 50, // Example distance
                StormType = "Thunderstorm",
                RadarImageUrl = await GetRadarImageUrlAsync(userLatitude, userLongitude)
            };

            return stormData;
        }

        public async Task<string> GetRadarImageUrlAsync(double latitude, double longitude)
        {
            // Get the nearest radar station
            var response = await _httpClient.GetAsync($"{_nwsApiBaseUrl}/radar/stations");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var stations = JsonSerializer.Deserialize<JsonElement>(content);

            // Find the nearest station and construct the radar image URL
            // This is a simplified example - actual implementation would need to find the nearest station
            return $"https://radar.weather.gov/ridge/standard/{latitude}_{longitude}_loop.gif";
        }
    }
}