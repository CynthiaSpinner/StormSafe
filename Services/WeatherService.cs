using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StormSafe.Models;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Net.Http.Headers;
using System.Collections.Generic;

namespace StormSafe.Services
{
    public interface IWeatherService
    {
        Task<StormData> GetStormDataAsync(double latitude, double longitude);
        Task<string> GetRadarImageUrlAsync(double latitude, double longitude, int zoom);
        Task<List<string>> GetRadarStationsAsync();
    }

    public class WeatherService : IWeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WeatherService> _logger;
        private readonly string _noaaBaseUrl;

        public WeatherService(
            ILogger<WeatherService> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("NOAA");
            _noaaBaseUrl = "https://api.weather.gov";

            // Add required headers for NOAA API
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "StormSafe/1.0 (https://github.com/yourusername/StormSafe; your.email@example.com)");
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/geo+json"));
        }

        public async Task<StormData> GetStormDataAsync(double latitude, double longitude)
        {
            try
            {
                _logger.LogInformation("Starting to fetch weather data for coordinates: {Latitude}, {Longitude}", latitude, longitude);

                // Get the grid point for the coordinates
                var gridPointUrl = $"{_noaaBaseUrl}/points/{latitude},{longitude}";
                _logger.LogInformation("Fetching grid point from: {GridPointUrl}", gridPointUrl);

                var gridPointResponse = await _httpClient.GetAsync(gridPointUrl);
                if (!gridPointResponse.IsSuccessStatusCode)
                {
                    var errorContent = await gridPointResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get grid point. Status: {Status}, Content: {Content}",
                        gridPointResponse.StatusCode, errorContent);
                    throw new Exception($"Failed to get grid point: {gridPointResponse.StatusCode}");
                }

                var gridPointContent = await gridPointResponse.Content.ReadAsStringAsync();
                _logger.LogInformation("Grid point response: {Response}", gridPointContent);

                var gridPoint = JsonSerializer.Deserialize<GridPointResponse>(gridPointContent);
                if (gridPoint?.Properties == null)
                {
                    _logger.LogError("Failed to deserialize grid point response or Properties is null");
                    throw new Exception("Failed to deserialize grid point response");
                }

                // Get current conditions
                var forecastUrl = gridPoint.Properties.Forecast;
                var observationStationsUrl = gridPoint.Properties.ObservationStations;

                if (string.IsNullOrEmpty(forecastUrl) || string.IsNullOrEmpty(observationStationsUrl))
                {
                    _logger.LogError("Missing required URLs in grid point response");
                    throw new Exception("Missing required URLs in grid point response");
                }

                _logger.LogInformation("Fetching forecast from: {ForecastUrl}", forecastUrl);
                _logger.LogInformation("Fetching observation stations from: {ObservationStationsUrl}", observationStationsUrl);

                var forecastResponse = await _httpClient.GetAsync(forecastUrl);
                if (!forecastResponse.IsSuccessStatusCode)
                {
                    var errorContent = await forecastResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get forecast. Status: {Status}, Content: {Content}",
                        forecastResponse.StatusCode, errorContent);
                    throw new Exception($"Failed to get forecast: {forecastResponse.StatusCode}");
                }

                var forecastContent = await forecastResponse.Content.ReadAsStringAsync();
                _logger.LogInformation("Forecast response: {Response}", forecastContent);

                var forecast = JsonSerializer.Deserialize<NOAAForecastResponse>(forecastContent);
                if (forecast?.Properties == null)
                {
                    _logger.LogError("Failed to deserialize forecast response or Properties is null");
                    throw new Exception("Failed to deserialize forecast response");
                }

                // Get current conditions from the first observation station
                var stationsResponse = await _httpClient.GetAsync(observationStationsUrl);
                if (!stationsResponse.IsSuccessStatusCode)
                {
                    var errorContent = await stationsResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get stations. Status: {Status}, Content: {Content}",
                        stationsResponse.StatusCode, errorContent);
                    throw new Exception($"Failed to get stations: {stationsResponse.StatusCode}");
                }

                var stationsContent = await stationsResponse.Content.ReadAsStringAsync();
                _logger.LogInformation("Stations response: {Response}", stationsContent);

                var stations = JsonSerializer.Deserialize<ObservationStationsResponse>(stationsContent);
                if (stations?.Features == null || !stations.Features.Any())
                {
                    _logger.LogError("No observation stations found");
                    throw new Exception("No observation stations found");
                }

                // Try to find a station with a valid latest observation URL
                var stationWithObservation = stations.Features.FirstOrDefault(s =>
                    s?.Properties?.LatestObservation != null &&
                    !string.IsNullOrEmpty(s.Properties.LatestObservation));

                if (stationWithObservation == null)
                {
                    _logger.LogWarning("No stations with valid observation URLs found, using default values");
                    // Create storm data with default values
                    return new StormData
                    {
                        CurrentLocation = new Location { Latitude = latitude, Longitude = longitude },
                        StormTypes = new List<string> { "Unknown" },
                        CurrentConditions = new CurrentConditions
                        {
                            Temperature = 0,
                            WindSpeed = 0,
                            WindDirection = 0,
                            Precipitation = 0,
                            Description = "No current conditions available"
                        },
                        Forecast = new List<ForecastPeriod>(),
                        PredictedPath = new List<Location>()
                    };
                }

                var latestObsUrl = stationWithObservation.Properties.LatestObservation;

                _logger.LogInformation("Fetching latest observation from: {LatestObsUrl}", latestObsUrl);

                // Set headers for latest observation request
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var conditionsResponse = await _httpClient.GetAsync(latestObsUrl);
                if (!conditionsResponse.IsSuccessStatusCode)
                {
                    var errorContent = await conditionsResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get conditions. Status: {Status}, Content: {Content}",
                        conditionsResponse.StatusCode, errorContent);
                    throw new Exception($"Failed to get conditions: {conditionsResponse.StatusCode}");
                }

                var conditionsContent = await conditionsResponse.Content.ReadAsStringAsync();
                _logger.LogInformation("Conditions response: {Response}", conditionsContent);

                var conditions = JsonSerializer.Deserialize<NOAAConditions>(conditionsContent);
                if (conditions?.Properties == null)
                {
                    _logger.LogError("Failed to deserialize conditions response or Properties is null");
                    throw new Exception("Failed to deserialize conditions response");
                }

                // Generate storm data with null checks
                var stormData = new StormData
                {
                    CurrentLocation = new Location { Latitude = latitude, Longitude = longitude },
                    StormTypes = DetermineStormTypes(
                        conditions.Properties.Temperature?.Value ?? 0,
                        conditions.Properties.WindSpeed?.Value ?? 0,
                        conditions.Properties.Precipitation?.Value ?? 0,
                        conditions.Properties.BarometricPressure?.Value ?? 0
                    ),
                    CurrentConditions = new CurrentConditions
                    {
                        Temperature = conditions.Properties.Temperature?.Value ?? 0,
                        WindSpeed = conditions.Properties.WindSpeed?.Value ?? 0,
                        WindDirection = conditions.Properties.WindDirection?.Value ?? 0,
                        Precipitation = conditions.Properties.Precipitation?.Value ?? 0,
                        Description = conditions.Properties.TextDescription ?? "Unknown"
                    },
                    Forecast = forecast.Properties.Periods?.Select(p => new ForecastPeriod
                    {
                        StartTime = DateTime.Parse(p.StartTime ?? DateTime.Now.ToString()),
                        EndTime = DateTime.Parse(p.EndTime ?? DateTime.Now.AddHours(1).ToString()),
                        Temperature = p.Temperature,
                        WindSpeed = double.Parse(p.WindSpeed?.Split(' ')[0] ?? "0"),
                        WindDirection = p.WindDirection ?? "N",
                        Description = p.ShortForecast ?? "Unknown",
                        DetailedForecast = p.DetailedForecast ?? "No detailed forecast available"
                    }).ToList() ?? new List<ForecastPeriod>(),
                    PredictedPath = GeneratePredictedPath(forecast.Properties.Periods ?? new List<NOAAPeriod>(), latitude, longitude)
                };

                _logger.LogInformation("Successfully retrieved storm data for coordinates: {Latitude}, {Longitude}", latitude, longitude);
                return stormData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching storm data for coordinates: {Latitude}, {Longitude}", latitude, longitude);
                throw;
            }
        }

        private double CalculateStormIntensity(double temperature, double windSpeed, double precipitation)
        {
            var windIntensity = Math.Min(1, windSpeed / 50.0); // Normalize wind speed (50 mph = max intensity)
            var precipitationIntensity = Math.Min(1, precipitation / 2.0); // Normalize precipitation (2 inches = max intensity)

            // Calculate combined intensity (0-100)
            return (windIntensity * 60 + precipitationIntensity * 40) * 100;
        }

        private List<string> DetermineStormTypes(double temperature, double windSpeed, double precipitation, double pressure)
        {
            var stormTypes = new List<string>();

            // Thunderstorm conditions
            if (temperature > 70 && precipitation > 0.5 && pressure < 1013)
            {
                stormTypes.Add("Thunderstorm");
            }

            // Rain storm conditions
            if (precipitation > 0.25)
            {
                stormTypes.Add("Rain Storm");
            }

            // Wind storm conditions
            if (windSpeed > 30)
            {
                stormTypes.Add("Wind Storm");
            }

            // Snow storm conditions
            if (temperature < 32 && precipitation > 0.1)
            {
                stormTypes.Add("Snow Storm");
            }

            return stormTypes;
        }

        private List<Location> GeneratePredictedPath(List<NOAAPeriod> periods, double latitude, double longitude)
        {
            var path = new List<Location>();
            var currentTime = DateTime.Now;

            if (!periods.Any())
            {
                return path;
            }

            // Calculate movement per hour (assuming wind speed is in mph)
            var firstPeriod = periods[0];
            var windSpeedStr = firstPeriod.WindSpeed?.Split(' ')[0] ?? "0";
            var windSpeed = double.Parse(windSpeedStr);
            var degreesPerHour = windSpeed / 69.0; // Approximate degrees per hour (1 degree ≈ 69 miles)

            for (int i = 0; i < periods.Count; i++)
            {
                var period = periods[i];
                var hours = i + 1;
                var distanceDegrees = degreesPerHour * hours;

                // Calculate new position
                var windDirection = ConvertWindDirectionToAngle(period.WindDirection ?? "N");
                var newLat = latitude + (distanceDegrees * Math.Cos(ToRadians(windDirection)));
                var newLon = longitude + (distanceDegrees * Math.Sin(ToRadians(windDirection)));

                path.Add(new Location
                {
                    Latitude = newLat,
                    Longitude = newLon
                });
            }

            return path;
        }

        private double ConvertWindDirectionToAngle(string direction)
        {
            return direction switch
            {
                "N" => 0,
                "NNE" => 22.5,
                "NE" => 45,
                "ENE" => 67.5,
                "E" => 90,
                "ESE" => 112.5,
                "SE" => 135,
                "SSE" => 157.5,
                "S" => 180,
                "SSW" => 202.5,
                "SW" => 225,
                "WSW" => 247.5,
                "W" => 270,
                "WNW" => 292.5,
                "NW" => 315,
                "NNW" => 337.5,
                _ => 0 // Default to North if unknown
            };
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

        private RadarStationFeature? FindNearestStation(List<RadarStationFeature>? stations, double latitude, double longitude)
        {
            if (stations == null || !stations.Any())
            {
                return null;
            }

            RadarStationFeature? nearestStation = null;
            double minDistance = double.MaxValue;

            foreach (var station in stations)
            {
                if (station?.Geometry?.Coordinates == null || station.Geometry.Coordinates.Count < 2)
                {
                    continue;
                }

                double distance = CalculateDistance(
                    latitude,
                    longitude,
                    station.Geometry.Coordinates[1],
                    station.Geometry.Coordinates[0]
                );

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestStation = station;
                }
            }

            return nearestStation;
        }

        public Task<string> GetRadarImageUrlAsync(double latitude, double longitude, int zoom)
        {
            try
            {
                _logger.LogInformation(
                    "Fetching radar image URL for lat: {Lat}, lon: {Lon}, zoom: {Zoom}",
                    latitude,
                    longitude,
                    zoom
                );

                // Calculate tile coordinates using Web Mercator projection
                // This matches OpenStreetMap's tile system
                double x = (longitude + 180.0) / 360.0 * Math.Pow(2, zoom);
                double y = (1.0 - Math.Log(Math.Tan(latitude * Math.PI / 180.0) +
                    1.0 / Math.Cos(latitude * Math.PI / 180.0)) / Math.PI) / 2.0 * Math.Pow(2, zoom);

                // Round to nearest tile
                int tileX = (int)Math.Floor(x);
                int tileY = (int)Math.Floor(y);

                // Construct the radar image URL using the IEM radar service
                // The URL format follows: https://mesonet.agron.iastate.edu/cache/tile.py/1.0.0/nexrad-n0q-900913/{z}/{x}/{y}.png
                // Note: The IEM service uses the same tile system as OpenStreetMap
                string url = $"https://mesonet.agron.iastate.edu/cache/tile.py/1.0.0/nexrad-n0q-900913/{zoom}/{tileX}/{tileY}.png";

                _logger.LogInformation("Generated radar URL: {Url}", url);
                return Task.FromResult(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating radar image URL");
                throw;
            }
        }

        public Task<List<string>> GetRadarStationsAsync()
        {
            var urls = new List<string>();
            // Logic to get radar station URLs
            return Task.FromResult(urls);
        }
    }
}