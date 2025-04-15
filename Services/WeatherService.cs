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
using System.Linq;
using System.IO;

namespace StormSafe.Services
{
    public interface IWeatherService
    {
        Task<StormData?> GetStormDataAsync(double latitude, double longitude);
        Task<StormData> GetStormDataByZipAsync(string zipCode);
        Task<StormData> GetStormDataByCityStateAsync(string city, string state);
        Task<string> GetRadarImageUrlAsync(double latitude, double longitude, int zoom);
        Task<List<string>> GetRadarStationsAsync();
        Task<List<ObservationStationFeature>> GetAllStationsAsync();
        Task<List<ObservationStationFeature>> GetNearestStationsAsync(double latitude, double longitude, int maxStations = 5);
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

        public async Task<StormData?> GetStormDataAsync(double latitude, double longitude)
        {
            try
            {
                _logger.LogInformation("Getting storm data for coordinates: {Latitude}, {Longitude}", latitude, longitude);

                var stations = await GetNearestStationsAsync(latitude, longitude);
                if (!stations.Any())
                {
                    _logger.LogWarning("No weather stations found near the specified coordinates");
                    return null;
                }

                StormData? localData = null;

                foreach (var station in stations)
                {
                    try
                    {
                        var distance = CalculateDistance(latitude, longitude,
                            station.Geometry.Coordinates[1], station.Geometry.Coordinates[0]);

                        string observationUrl = $"https://api.weather.gov/stations/{station.Properties.StationIdentifier}/observations/latest";
                        _logger.LogInformation("Fetching observation data from: {Url}", observationUrl);

                        var observationResponse = await _httpClient.GetAsync(observationUrl);

                        if (!observationResponse.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Failed to get observation data for station {StationId}, status: {StatusCode}",
                                station.Properties.StationIdentifier,
                                observationResponse.StatusCode);
                            continue;
                        }

                        var observationContent = await observationResponse.Content.ReadAsStringAsync();
                        _logger.LogInformation("Received observation data: {Content}", observationContent);

                        var observation = JsonSerializer.Deserialize<ObservationResponse>(observationContent);

                        if (observation?.Properties == null)
                        {
                            _logger.LogWarning("Invalid observation data for station {StationId}", station.Properties.StationIdentifier);
                            continue;
                        }

                        // Check for storm indicators
                        var windSpeed = observation.Properties.WindSpeed?.Value ?? 0;
                        var temperature = observation.Properties.Temperature?.Value ?? 0;
                        var timestamp = observation.Properties.Timestamp;
                        var windDirection = observation.Properties.WindDirection?.Value ?? 0;
                        var dewpoint = observation.Properties.Dewpoint?.Value ?? 0;

                        // If we find storm indicators, update the local data with storm information
                        if (windSpeed > 30)
                        {
                            _logger.LogInformation("Found storm data at station {StationId}", station.Properties.StationIdentifier);

                            var stormIntensity = CalculateStormIntensity(windSpeed);
                            var stormType = DetermineStormType(windSpeed);
                            var stormDescription = $"High winds detected at {station.Properties.Name}";

                            localData = new StormData
                            {
                                Temperature = temperature,
                                WindSpeed = windSpeed,
                                WindDirection = windDirection,
                                Dewpoint = dewpoint,
                                StationId = station.Properties.StationIdentifier,
                                StationName = station.Properties.Name,
                                Distance = distance,
                                Timestamp = timestamp,
                                LastUpdated = DateTime.UtcNow,
                                CurrentConditions = new CurrentConditions
                                {
                                    Temperature = temperature,
                                    WindSpeed = windSpeed,
                                    WindDirection = windDirection.ToString(),
                                    Dewpoint = dewpoint,
                                    Precipitation = 0, // Default value
                                    Pressure = 0, // Default value
                                    Visibility = 0, // Default value
                                    CloudCover = 0, // Default value
                                    Humidity = 0, // Default value
                                    StormType = stormType,
                                    StormIntensity = stormIntensity,
                                    StormDescription = stormDescription,
                                    StationName = station.Properties.Name,
                                    StationDistance = distance
                                }
                            };
                            break;
                        }
                        else
                        {
                            _logger.LogInformation("No storm data found at station {StationId}", station.Properties.StationIdentifier);
                            localData = new StormData
                            {
                                Temperature = temperature,
                                WindSpeed = windSpeed,
                                WindDirection = windDirection,
                                Dewpoint = dewpoint,
                                StationId = station.Properties.StationIdentifier,
                                StationName = station.Properties.Name,
                                Distance = distance,
                                Timestamp = timestamp,
                                LastUpdated = DateTime.UtcNow,
                                CurrentConditions = new CurrentConditions
                                {
                                    Temperature = temperature,
                                    WindSpeed = windSpeed,
                                    WindDirection = windDirection.ToString(),
                                    Dewpoint = dewpoint,
                                    Precipitation = 0, // Default value
                                    Pressure = 0, // Default value
                                    Visibility = 0, // Default value
                                    CloudCover = 0, // Default value
                                    Humidity = 0, // Default value
                                    StormType = "No Storm",
                                    StormIntensity = 0,
                                    StormDescription = "No storm activity detected",
                                    StationName = station.Properties.Name,
                                    StationDistance = distance
                                }
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error getting data from station {StationId}", station.Properties.StationIdentifier);
                    }
                }

                if (localData == null)
                {
                    _logger.LogWarning("No valid data found from any nearby stations");
                    return null;
                }

                return localData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting storm data");
                return null;
            }
        }

        private int CalculateStormIntensity(double windSpeed)
        {
            // Calculate storm intensity based on wind speed
            if (windSpeed >= 120) return 5; // Hurricane force winds
            if (windSpeed >= 90) return 4;  // Storm force winds
            if (windSpeed >= 60) return 3;  // Gale force winds
            if (windSpeed >= 45) return 2;  // Strong winds
            if (windSpeed >= 30) return 1;  // Moderate winds
            return 0;                       // No storm
        }

        private string DetermineStormType(double windSpeed)
        {
            if (windSpeed >= 120) return "Hurricane Force Winds";
            if (windSpeed >= 90) return "Storm Force Winds";
            if (windSpeed >= 60) return "Gale Force Winds";
            if (windSpeed >= 45) return "Strong Winds";
            if (windSpeed >= 30) return "Moderate Winds";
            return "No Storm";
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 3958.8; // Earth's radius in miles
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
                var stormData = await GetStormDataAsync(location.Lat, location.Lon);

                if (stormData == null)
                {
                    throw new Exception($"No storm data found for ZIP code: {zipCode}");
                }

                return stormData;
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
                var stormData = await GetStormDataAsync(location.Lat, location.Lon);

                if (stormData == null)
                {
                    throw new Exception($"No storm data found for city: {city}, state: {state}");
                }

                return stormData;
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
                CurrentConditions = new CurrentConditions
                {
                    Temperature = 0,
                    WindSpeed = 0,
                    WindDirection = "N",
                    Precipitation = 0,
                    Pressure = 0,
                    Visibility = 0,
                    CloudCover = 0,
                    Humidity = 0,
                    StormType = "None",
                    StormIntensity = 0,
                    StormDescription = "No current conditions available"
                },
                ForecastPeriods = new List<ForecastPeriod>(),
                Alerts = new List<WeatherAlert>(),
                LastUpdated = DateTime.UtcNow
            };
        }

        public async Task<List<ObservationStationFeature>> GetAllStationsAsync()
        {
            try
            {
                var stationsUrl = $"{_noaaBaseUrl}/stations";
                _logger.LogInformation("Fetching stations from: {StationsUrl}", stationsUrl);

                var response = await _httpClient.GetAsync(stationsUrl);
                _logger.LogInformation("Stations API response status: {StatusCode}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get stations list, status code: {StatusCode}", response.StatusCode);
                    return new List<ObservationStationFeature>();
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Stations API response content length: {Length}", content.Length);

                var stationsResponse = JsonSerializer.Deserialize<ObservationStationsResponse>(content);

                if (stationsResponse?.Features == null)
                {
                    _logger.LogWarning("Invalid stations response");
                    return new List<ObservationStationFeature>();
                }

                return stationsResponse.Features;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stations list");
                return new List<ObservationStationFeature>();
            }
        }

        public async Task<List<ObservationStationFeature>> GetNearestStationsAsync(double latitude, double longitude, int maxStations = 5)
        {
            try
            {
                var allStations = await GetAllStationsAsync();
                var stationsWithDistance = new List<(ObservationStationFeature Station, double Distance)>();

                foreach (var station in allStations)
                {
                    if (station.Geometry?.Coordinates != null && station.Geometry.Coordinates.Count >= 2)
                    {
                        var stationLat = station.Geometry.Coordinates[1];
                        var stationLon = station.Geometry.Coordinates[0];
                        var distance = Geospatial.CalculateDistance(latitude, longitude, stationLat, stationLon);
                        stationsWithDistance.Add((station, distance));
                    }
                }

                // Sort by distance and take the nearest stations
                return stationsWithDistance
                    .OrderBy(x => x.Distance)
                    .Take(maxStations)
                    .Select(x => x.Station)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding nearest stations for coordinates: {Latitude}, {Longitude}", latitude, longitude);
                return new List<ObservationStationFeature>();
            }
        }
    }
}