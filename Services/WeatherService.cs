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
        Task<RadarImageResponse> GetRadarImageUrl(double latitude, double longitude);
        Task<RadarStationsResponse> GetRadarStationsAsync();
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

                var firstStation = stations.Features.First();
                if (firstStation.Properties?.LatestObservation == null)
                {
                    _logger.LogError("Latest observation URL is null");
                    throw new Exception("Latest observation URL is null");
                }

                var latestObsUrl = firstStation.Properties.LatestObservation;

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

        private string GetWeatherIcon(string forecast)
        {
            forecast = forecast.ToLower();
            return forecast switch
            {
                var f when f.Contains("thunderstorm") => "11d",
                var f when f.Contains("rain") => "10d",
                var f when f.Contains("snow") => "13d",
                var f when f.Contains("cloud") => "03d",
                _ => "01d"
            };
        }

        private double CalculateIntensity(NOAAConditionsProperties conditions)
        {
            var precipitationIntensity = conditions.Precipitation?.Value ?? 0;
            var windIntensity = (conditions.WindSpeed?.Value ?? 0) / 50.0;

            return Math.Min(100, (precipitationIntensity * 20 + windIntensity * 50));
        }

        private string GetStormType(NOAAConditionsProperties conditions)
        {
            var forecast = conditions.TextDescription?.ToLower() ?? "unknown";
            return forecast switch
            {
                var f when f.Contains("thunderstorm") => "Thunderstorm",
                var f when f.Contains("rain") => "Rain Storm",
                var f when f.Contains("snow") => "Snow Storm",
                var f when f.Contains("drizzle") => "Light Rain",
                _ => "Weather System"
            };
        }

        private string GetAlertLevel(NOAAConditionsProperties conditions)
        {
            var windSpeed = conditions.WindSpeed?.Value ?? 0;
            var precipitation = conditions.Precipitation?.Value ?? 0;

            if (windSpeed > 50 || precipitation > 2)
                return "Warning";
            if (windSpeed > 30 || precipitation > 1)
                return "Watch";
            return "Advisory";
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

        private List<StormPathPoint> GeneratePredictedPath(List<NOAAPeriod> periods, double latitude, double longitude)
        {
            var path = new List<StormPathPoint>();
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

                path.Add(new StormPathPoint
                {
                    Latitude = newLat,
                    Longitude = newLon,
                    Time = currentTime.AddHours(hours),
                    Intensity = CalculateStormIntensity(
                        period.Temperature,
                        double.Parse(period.WindSpeed?.Split(' ')[0] ?? "0"),
                        0 // Precipitation not available in forecast periods
                    )
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

        private DateTime CalculateEstimatedArrivalTime(List<NOAAPeriod> periods, NOAAConditionsProperties conditions)
        {
            var currentIntensity = CalculateIntensity(conditions);

            foreach (var period in periods)
            {
                var forecastIntensity = CalculateIntensity(period);

                if (forecastIntensity > 30 ||
                    (period.ShortForecast != conditions.TextDescription &&
                     (period.ShortForecast?.Contains("Rain") == true || period.ShortForecast?.Contains("Thunderstorm") == true)))
                {
                    return DateTime.Parse(period.StartTime ?? DateTime.Now.ToString());
                }
            }

            return DateTime.Now.AddHours(2);
        }

        private double CalculateIntensity(NOAAPeriod period)
        {
            // Since precipitation is not available in forecast periods, we'll use wind speed only
            var windSpeed = double.Parse(period.WindSpeed?.Split(' ')[0] ?? "0");
            var windIntensity = windSpeed / 50.0;
            return Math.Min(100, windIntensity * 100);
        }

        private bool IsStormPresent(NOAAConditionsProperties conditions, List<NOAAPeriod> periods)
        {
            // Check current conditions
            if (conditions.TextDescription != null)
            {
                if (conditions.TextDescription.Contains("Thunderstorm") ||
                    conditions.TextDescription.Contains("Rain") ||
                    conditions.TextDescription.Contains("Snow"))
                {
                    return true;
                }
            }

            // Check forecast
            foreach (var period in periods)
            {
                if (period.ShortForecast != null)
                {
                    if (period.ShortForecast.Contains("Thunderstorm") ||
                        period.ShortForecast.Contains("Rain") ||
                        period.ShortForecast.Contains("Snow"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public async Task<RadarImageResponse> GetRadarImageUrl(double latitude, double longitude)
        {
            try
            {
                _logger.LogInformation("Getting radar image URL for coordinates: {Latitude}, {Longitude}", latitude, longitude);

                // Get the nearest radar station
                var stations = await GetRadarStationsAsync();
                var nearestStation = FindNearestStation(stations.Features, latitude, longitude);

                if (nearestStation == null)
                {
                    _logger.LogWarning("No operational radar stations found near coordinates: {Latitude}, {Longitude}", latitude, longitude);
                    return new RadarImageResponse { Url = string.Empty };
                }

                // Use the NCEP OpenGeo WMS service for radar tiles
                var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                var url = $"https://opengeo.ncep.noaa.gov/geoserver/conus/conus_bref_qcd/ows?service=WMS&version=1.3.0&request=GetMap&layers=conus_bref_qcd&styles=&bbox={{bbox}}&width=256&height=256&crs=EPSG:3857&format=image/png&time={timestamp}";

                _logger.LogInformation("Generated radar image URL: {Url}", url);
                return new RadarImageResponse { Url = url };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting radar image URL for coordinates: {Latitude}, {Longitude}", latitude, longitude);
                return new RadarImageResponse { Url = string.Empty };
            }
        }

        public async Task<RadarStationsResponse> GetRadarStationsAsync()
        {
            try
            {
                _logger.LogInformation("Fetching radar stations from NOAA API");
                var response = await _httpClient.GetAsync($"{_noaaBaseUrl}/radar/stations");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get radar stations. Status: {StatusCode}, Content: {Content}",
                        response.StatusCode, errorContent);
                    throw new Exception($"Failed to get radar stations: {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Radar stations response: {Response}", content);

                var stations = JsonSerializer.Deserialize<RadarStationsResponse>(content);
                if (stations?.Features == null)
                {
                    _logger.LogError("Failed to deserialize radar stations response or Features is null");
                    throw new Exception("Failed to deserialize radar stations response");
                }

                // Filter for operational stations
                var operationalStations = stations.Features.Where(f =>
                    f.Properties?.Rda?.Properties?.OperabilityStatus == "OPERATIONAL" &&
                    f.Properties.Rda.Properties.AlarmSummary == "None" &&
                    f.Properties.Rda.Properties.Mode == "Clear Air").ToList();

                _logger.LogInformation("Found {Count} operational radar stations", operationalStations.Count);
                return new RadarStationsResponse { Features = operationalStations };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching radar stations");
                throw;
            }
        }

        private async Task<string> GetBestRadarStation(double latitude, double longitude)
        {
            try
            {
                var response = await _httpClient.GetAsync("https://api.weather.gov/radar/stations");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Radar stations response: {Response}", content);

                var stations = JsonSerializer.Deserialize<RadarStationsResponse>(content);

                if (stations?.Features == null)
                {
                    _logger.LogError("Failed to deserialize radar stations response or Features is null");
                    throw new InvalidOperationException("Failed to get radar stations data");
                }

                var operationalStations = stations.Features
                    .Where(s => s?.Properties?.Rda?.Properties?.OperabilityStatus == "RDA - On-line")
                    .Where(s => s?.Properties?.Rda?.Properties?.AlarmSummary == "No Alarms")
                    .Where(s => s?.Properties?.Rda?.Properties?.Mode == "Operational")
                    .ToList();

                if (!operationalStations.Any())
                {
                    _logger.LogWarning("No operational radar stations found");
                    return "KOKX"; // Default to New York
                }

                var closestStation = operationalStations
                    .OrderBy(s =>
                    {
                        if (s?.Geometry?.Coordinates == null || s.Geometry.Coordinates.Count < 2)
                        {
                            return double.MaxValue;
                        }
                        var lat2 = Convert.ToDouble(s.Geometry.Coordinates[1]);
                        var lon2 = Convert.ToDouble(s.Geometry.Coordinates[0]);
                        return CalculateDistance(latitude, longitude, lat2, lon2);
                    })
                    .FirstOrDefault();

                if (closestStation?.Properties?.Id == null)
                {
                    _logger.LogWarning("No valid radar station found");
                    return "KOKX"; // Default to New York
                }

                _logger.LogInformation($"Selected radar station: {closestStation.Properties.Id}");
                return closestStation.Properties.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting best radar station");
                return "KOKX"; // Default to New York
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
    }
}