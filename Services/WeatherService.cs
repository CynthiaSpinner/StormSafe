using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StormSafe.Models;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Collections.Generic;

namespace StormSafe.Services
{
    public interface IWeatherService
    {
        Task<StormData> GetStormDataAsync(double latitude, double longitude);
        Task<StormData> GetStormDataByZipAsync(string zipCode);
        Task<StormData> GetStormDataByCityStateAsync(string city, string state);
        Task<string> GetRadarImageUrlAsync(double latitude, double longitude, int zoom);
        Task<List<string>> GetRadarStationsAsync();
    }

    public class WeatherService : IWeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WeatherService> _logger;
        private readonly string _noaaBaseUrl;
        private readonly string _nominatimBaseUrl;

        public WeatherService(HttpClient httpClient, ILogger<WeatherService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _noaaBaseUrl = "https://api.weather.gov";
            _nominatimBaseUrl = "https://nominatim.openstreetmap.org";
        }

        public async Task<StormData> GetStormDataAsync(double latitude, double longitude)
        {
            try
            {
                _logger.LogInformation("Starting to fetch weather data for coordinates: {Latitude}, {Longitude}", latitude, longitude);

                // Get the grid point for the coordinates
                var gridPointUrl = $"{_noaaBaseUrl}/points/{latitude},{longitude}";
                _logger.LogInformation("Fetching grid point from: {GridPointUrl}", gridPointUrl);

                try
                {
                    // Get grid point data
                    var gridPointResponse = await _httpClient.GetAsync(gridPointUrl);
                    if (!gridPointResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await gridPointResponse.Content.ReadAsStringAsync();
                        _logger.LogError("Failed to get grid point data: {StatusCode}, Content: {Content}",
                            gridPointResponse.StatusCode, errorContent);
                        throw new Exception($"Failed to get grid point data: {gridPointResponse.StatusCode} - {errorContent}");
                    }

                    var gridPointContent = await gridPointResponse.Content.ReadAsStringAsync();
                    _logger.LogInformation("Grid point response: {Content}", gridPointContent);

                    var gridPoint = JsonSerializer.Deserialize<GridPointResponse>(gridPointContent);
                    if (gridPoint?.Properties == null)
                    {
                        _logger.LogError("Failed to deserialize grid point response or Properties is null. Content: {Content}",
                            gridPointContent);
                        throw new Exception("Failed to deserialize grid point response");
                    }

                    _logger.LogInformation("Grid point data - Forecast URL: {ForecastUrl}, Stations URL: {StationsUrl}",
                        gridPoint.Properties.Forecast, gridPoint.Properties.ObservationStations);

                    // Get observation stations
                    var stationsResponse = await _httpClient.GetAsync(gridPoint.Properties.ObservationStations);
                    if (!stationsResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError("Failed to get observation stations: {StatusCode}", stationsResponse.StatusCode);
                        throw new Exception($"Failed to get observation stations: {stationsResponse.StatusCode}");
                    }

                    var stationsContent = await stationsResponse.Content.ReadAsStringAsync();
                    _logger.LogInformation("Stations response: {Content}", stationsContent);

                    var stations = JsonSerializer.Deserialize<ObservationStationsResponse>(stationsContent);
                    if (stations?.Features == null || !stations.Features.Any())
                    {
                        _logger.LogError("No observation stations found");
                        throw new Exception("No observation stations found");
                    }

                    // Get latest observation
                    var latestObservationUrl = stations.Features[0].Properties.LatestObservation;
                    _logger.LogInformation("Fetching latest observation from: {Url}", latestObservationUrl);

                    var conditionsResponse = await _httpClient.GetAsync(latestObservationUrl);
                    if (!conditionsResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError("Failed to get conditions: {StatusCode}", conditionsResponse.StatusCode);
                        throw new Exception($"Failed to get conditions: {conditionsResponse.StatusCode}");
                    }

                    var conditionsContent = await conditionsResponse.Content.ReadAsStringAsync();
                    _logger.LogInformation("Conditions response: {Content}", conditionsContent);

                    var conditions = JsonSerializer.Deserialize<NOAAConditions>(conditionsContent);
                    if (conditions?.Properties == null)
                    {
                        _logger.LogError("Failed to deserialize conditions response or Properties is null");
                        throw new Exception("Failed to deserialize conditions response");
                    }

                    // Fetch forecast data
                    var forecastUrl = gridPoint.Properties.Forecast;
                    if (string.IsNullOrEmpty(forecastUrl))
                    {
                        _logger.LogError("Forecast URL is null or empty");
                        throw new Exception("Forecast URL is missing");
                    }

                    var forecastResponse = await _httpClient.GetAsync(forecastUrl);
                    if (!forecastResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError("Failed to fetch forecast data: {StatusCode}", forecastResponse.StatusCode);
                        throw new Exception($"Failed to fetch forecast data: {forecastResponse.StatusCode}");
                    }

                    var forecastContent = await forecastResponse.Content.ReadAsStringAsync();
                    var forecast = JsonSerializer.Deserialize<NOAAForecastResponse>(forecastContent);
                    if (forecast?.Properties == null)
                    {
                        _logger.LogError("Failed to deserialize forecast response or Properties is null");
                        throw new Exception("Failed to deserialize forecast response");
                    }

                    // Fetch alerts data
                    var alertsUrl = gridPoint.Properties.Alerts;
                    var alerts = new List<WeatherAlert>();
                    if (!string.IsNullOrEmpty(alertsUrl))
                    {
                        var alertsResponse = await _httpClient.GetAsync(alertsUrl);
                        if (alertsResponse.IsSuccessStatusCode)
                        {
                            var alertsContent = await alertsResponse.Content.ReadAsStringAsync();
                            var alertsData = JsonSerializer.Deserialize<NOAAAlertsResponse>(alertsContent);
                            if (alertsData?.Features != null)
                            {
                                alerts = alertsData.Features.Select(f => new WeatherAlert
                                {
                                    Event = f.Properties?.Event ?? "Unknown",
                                    Severity = f.Properties?.Severity ?? "Unknown",
                                    Urgency = f.Properties?.Urgency ?? "Unknown",
                                    Description = f.Properties?.Description ?? "No description available",
                                    StartTime = DateTime.Parse(f.Properties?.Effective ?? DateTime.Now.ToString()),
                                    EndTime = DateTime.Parse(f.Properties?.Expires ?? DateTime.Now.AddHours(1).ToString())
                                }).ToList();
                            }
                        }
                    }

                    _logger.LogInformation("Raw conditions data - Temp: {TempC}°C, Wind: {WindKmh} km/h, Precip: {PrecipMm} mm",
                        conditions.Properties.Temperature?.Value,
                        conditions.Properties.WindSpeed?.Value,
                        conditions.Properties.PrecipitationLastHour?.Value);

                    // Convert units from metric to imperial
                    var tempC = conditions.Properties.Temperature?.Value ?? 0;
                    var windSpeedKmh = conditions.Properties.WindSpeed?.Value ?? 0;
                    var precipMm = conditions.Properties.PrecipitationLastHour?.Value ?? 0;
                    var visibilityMeters = conditions.Properties.Visibility?.Value ?? 0;
                    var windDirection = conditions.Properties.WindDirection?.Value ?? 0;
                    var windGustKmh = conditions.Properties.WindGust?.Value ?? 0;
                    var pressurePa = conditions.Properties.BarometricPressure?.Value ?? 0;

                    // Convert temperature from Celsius to Fahrenheit
                    var tempF = (tempC * 9 / 5) + 32;

                    // Convert wind speed from km/h to mph
                    var windSpeedMph = windSpeedKmh * 0.621371;
                    var windGustMph = windGustKmh * 0.621371;

                    // Convert precipitation from mm to inches
                    var precipInches = precipMm * 0.0393701;

                    // Convert visibility from meters to miles
                    var visibilityMiles = visibilityMeters * 0.000621371;

                    // Convert pressure from Pa to hPa (millibars)
                    var pressureHpa = pressurePa / 100;

                    _logger.LogInformation("Converted weather data - Temp: {TempF}°F, Wind: {WindMph} mph, Precip: {PrecipInches} in",
                        tempF, windSpeedMph, precipInches);

                    // Generate storm data with null checks
                    var stormData = new StormData
                    {
                        CurrentLocation = new Location { Latitude = latitude, Longitude = longitude },
                        StormTypes = DetermineStormTypes(tempF, windSpeedMph, precipInches),
                        CurrentConditions = new CurrentConditions
                        {
                            Temperature = Math.Round(tempF, 1),
                            WindSpeed = Math.Round(windSpeedMph, 1),
                            WindDirection = windDirection,
                            Precipitation = Math.Round(precipInches, 2),
                            Description = conditions.Properties.TextDescription ?? "No current conditions available"
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
                        PredictedPath = GeneratePredictedPath(forecast.Properties.Periods ?? new List<NOAAPeriod>(), latitude, longitude),
                        Alerts = alerts
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching storm data for coordinates: {Latitude}, {Longitude}", latitude, longitude);
                throw;
            }
        }

        private double CalculateStormIntensity(double temperature, double windSpeed, double precipitation)
        {
            // Normalize wind speed (using NOAA's wind thresholds)
            var windIntensity = windSpeed switch
            {
                var w when w > 74 => 1.0,    // Hurricane force
                var w when w > 58 => 0.8,    // High wind
                var w when w > 40 => 0.6,    // Strong wind
                var w when w > 30 => 0.4,    // Wind storm
                _ => Math.Min(1, windSpeed / 30.0) // Linear scale for lower winds
            };

            // Normalize precipitation (using NOAA's precipitation thresholds)
            var precipitationIntensity = precipitation switch
            {
                var p when p > 4.0 => 1.0,   // Heavy snow/rain
                var p when p > 2.0 => 0.8,   // Moderate snow/rain
                var p when p > 1.0 => 0.6,   // Light snow/rain
                _ => Math.Min(1, precipitation / 1.0) // Linear scale for lower amounts
            };

            // Calculate combined intensity (0-100)
            // Weight wind more heavily for high winds, precipitation more for heavy precipitation
            var windWeight = windSpeed > 40 ? 0.7 : 0.5;
            var precipitationWeight = precipitation > 2.0 ? 0.7 : 0.5;

            return (windIntensity * windWeight + precipitationIntensity * precipitationWeight) * 100;
        }

        private List<string> DetermineStormTypes(double temperature, double windSpeed, double precipitation)
        {
            var stormTypes = new List<string>();

            // Thunderstorm conditions (using NOAA's criteria)
            if (temperature > 70 && precipitation > 0.5)
            {
                if (windSpeed > 58) // Severe thunderstorm criteria
                {
                    stormTypes.Add("Severe Thunderstorm");
                }
                else
                {
                    stormTypes.Add("Thunderstorm");
                }
            }

            // Rain storm conditions (using NOAA's precipitation thresholds)
            if (precipitation > 0.25)
            {
                if (precipitation > 2.0) // Heavy rain criteria
                {
                    stormTypes.Add("Heavy Rain Storm");
                }
                else if (precipitation > 1.0) // Moderate rain criteria
                {
                    stormTypes.Add("Moderate Rain Storm");
                }
                else
                {
                    stormTypes.Add("Light Rain Storm");
                }
            }

            // Wind storm conditions (using NOAA's wind thresholds)
            if (windSpeed > 30)
            {
                if (windSpeed > 58) // High wind criteria
                {
                    stormTypes.Add("High Wind Storm");
                }
                else if (windSpeed > 40) // Strong wind criteria
                {
                    stormTypes.Add("Strong Wind Storm");
                }
                else
                {
                    stormTypes.Add("Wind Storm");
                }
            }

            // Snow storm conditions (using NOAA's winter storm criteria)
            if (temperature < 32 && precipitation > 0.1)
            {
                if (precipitation > 4.0) // Heavy snow criteria
                {
                    stormTypes.Add("Heavy Snow Storm");
                }
                else if (precipitation > 2.0) // Moderate snow criteria
                {
                    stormTypes.Add("Moderate Snow Storm");
                }
                else
                {
                    stormTypes.Add("Light Snow Storm");
                }
            }

            // Ice storm conditions
            if (temperature > 25 && temperature < 32 && precipitation > 0.1)
            {
                stormTypes.Add("Ice Storm");
            }

            // Tropical storm conditions (simplified criteria)
            if (temperature > 80 && windSpeed > 39 && precipitation > 1.0)
            {
                if (windSpeed > 74) // Hurricane criteria
                {
                    stormTypes.Add("Hurricane");
                }
                else
                {
                    stormTypes.Add("Tropical Storm");
                }
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

        public async Task<StormData> GetStormDataByZipAsync(string zipCode)
        {
            try
            {
                _logger.LogInformation("Looking up coordinates for ZIP code: {ZipCode}", zipCode);

                // Use OpenStreetMap's Nominatim service to get coordinates
                var response = await _httpClient.GetAsync($"{_nominatimBaseUrl}/search?postalcode={zipCode}&country=US&format=json&limit=1");
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get coordinates for ZIP code: {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                var locations = JsonSerializer.Deserialize<List<NominatimLocation>>(content);

                if (locations == null || !locations.Any())
                {
                    throw new Exception($"No location found for ZIP code: {zipCode}");
                }

                var location = locations[0];
                return await GetStormDataAsync(location.Lat, location.Lon);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storm data for ZIP code: {ZipCode}", zipCode);
                throw;
            }
        }

        public async Task<StormData> GetStormDataByCityStateAsync(string city, string state)
        {
            try
            {
                _logger.LogInformation("Looking up coordinates for city: {City}, state: {State}", city, state);

                // Use OpenStreetMap's Nominatim service to get coordinates
                var query = Uri.EscapeDataString($"{city}, {state}, USA");
                var response = await _httpClient.GetAsync($"{_nominatimBaseUrl}/search?q={query}&format=json&limit=1");
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get coordinates for city/state: {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                var locations = JsonSerializer.Deserialize<List<NominatimLocation>>(content);

                if (locations == null || !locations.Any())
                {
                    throw new Exception($"No location found for city: {city}, state: {state}");
                }

                var location = locations[0];
                return await GetStormDataAsync(location.Lat, location.Lon);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storm data for city: {City}, state: {State}", city, state);
                throw;
            }
        }

        // Default values for when data is unavailable
        private StormData GetDefaultStormData(double latitude, double longitude)
        {
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
                PredictedPath = new List<Location>(),
                Alerts = new List<WeatherAlert>()
            };
        }
    }
}