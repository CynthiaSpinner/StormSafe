using Microsoft.AspNetCore.Mvc;
using StormSafe.Services;
using StormSafe.Models;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.Caching;
using System.Net.Http.Headers;
using System.Text.Json;
using System;
using System.Linq;

namespace StormSafe.Controllers
{
    /// <summary>
    /// Controller for handling weather-related API endpoints
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly IWeatherService _weatherService;
        private readonly ILogger<WeatherController> _logger;
        private readonly IMemoryCache _cache;
        private readonly IAsyncPolicy<byte[]> _retryPolicy;
        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClientFactory;

        public WeatherController(
            IWeatherService weatherService,
            ILogger<WeatherController> logger,
            IMemoryCache cache,
            IHttpClientFactory httpClientFactory)
        {
            _weatherService = weatherService;
            _logger = logger;
            _cache = cache;
            _httpClient = httpClientFactory.CreateClient("WeatherAPI");
            _httpClientFactory = httpClientFactory;

            // Configure Polly retry policy
            _retryPolicy = Policy<byte[]>
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            "Retry {RetryCount} after {RetryTime}s delay",
                            retryCount,
                            timeSpan.TotalSeconds);
                    });
        }

        /// <summary>
        /// Gets storm data for a specific location
        /// </summary>
        /// <param name="latitude">The latitude coordinate (e.g., 40.7128 for New York)</param>
        /// <param name="longitude">The longitude coordinate (e.g., -74.0060 for New York)</param>
        /// <returns>Storm data including current conditions and predictions</returns>
        /// <response code="200">Returns the storm data</response>
        /// <response code="500">If there was an error fetching the data</response>
        [HttpGet("storm-data")]
        [ProducesResponseType(typeof(StormData), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<StormData>> GetStormData(
            [FromQuery, Required] double latitude,
            [FromQuery, Required] double longitude)
        {
            try
            {
                _logger.LogInformation("Fetching storm data for coordinates: lat={Latitude}, lon={Longitude}", latitude, longitude);

                var stormData = await _weatherService.GetStormDataAsync(latitude, longitude);
                if (stormData == null)
                {
                    _logger.LogWarning("No storm data returned for coordinates: lat={Latitude}, lon={Longitude}", latitude, longitude);
                    return StatusCode(500, new { error = "No storm data available for the specified location" });
                }

                _logger.LogInformation("Successfully retrieved storm data for coordinates: lat={Latitude}, lon={Longitude}", latitude, longitude);
                return Ok(stormData);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed for coordinates: lat={Latitude}, lon={Longitude}", latitude, longitude);
                return StatusCode(500, new { error = "Failed to fetch weather data from external service" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching storm data for coordinates: lat={Latitude}, lon={Longitude}", latitude, longitude);
                return StatusCode(500, new { error = "An unexpected error occurred while fetching storm data" });
            }
        }

        /// <summary>
        /// Gets the radar image URL for a specific location
        /// </summary>
        /// <param name="latitude">The latitude coordinate (e.g., 40.7128 for New York)</param>
        /// <param name="longitude">The longitude coordinate (e.g., -74.0060 for New York)</param>
        /// <returns>The URL of the radar image</returns>
        /// <response code="200">Returns the radar image URL</response>
        /// <response code="500">If there was an error fetching the URL</response>
        [HttpGet("radar-image")]
        [ProducesResponseType(typeof(RadarImageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRadarImage([FromQuery] double latitude, [FromQuery] double longitude)
        {
            try
            {
                var response = await _weatherService.GetRadarImageUrl(latitude, longitude);
                if (string.IsNullOrEmpty(response.Url))
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to get radar image URL");
                }
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting radar image");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while getting the radar image");
            }
        }

        [HttpGet("radar/{station}/{product}")]
        public async Task<IActionResult> GetRadarImage(
            string station,
            string product,
            [FromQuery] int? x = null,
            [FromQuery] int? y = null,
            [FromQuery] int? z = null,
            [FromQuery] long? t = null)
        {
            try
            {
                _logger.LogInformation("Fetching radar image for station {Station}, product {Product}", station, product);

                // For N0R (base reflectivity), try multiple URL formats
                if (product.Equals("N0R", StringComparison.OrdinalIgnoreCase))
                {
                    using var client = _httpClientFactory.CreateClient();
                    client.DefaultRequestHeaders.Add("User-Agent", "StormSafe/1.0");

                    // Try formats in order of preference
                    var urlFormats = new[]
                    {
                        $"https://radar.weather.gov/ridge/standard/{station}/N0R/latest.gif",
                        $"https://radar.weather.gov/ridge/RadarImg/{product}/{station}/{station}_{product}_0.gif",
                        $"https://radar.weather.gov/ridge/standard/{station}_{product}.gif"
                    };

                    foreach (var url in urlFormats)
                    {
                        _logger.LogInformation("Trying radar URL: {Url}", url);
                        try
                        {
                            var response = await client.GetAsync(url);
                            if (response.IsSuccessStatusCode)
                            {
                                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                                return File(imageBytes, "image/gif");
                            }
                            else
                            {
                                _logger.LogWarning("Failed to fetch from {Url}. Status: {Status}",
                                    url, response.StatusCode);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error fetching from {Url}", url);
                        }
                    }

                    _logger.LogWarning("All URL formats failed for station {Station}", station);
                    return NotFound();
                }

                // For other products, use the standard format
                var standardUrl = $"https://radar.weather.gov/ridge/standard/{station}/{product}/latest.gif";
                _logger.LogInformation("Fetching standard radar image from: {Url}", standardUrl);

                try
                {
                    using var client = _httpClientFactory.CreateClient();
                    client.DefaultRequestHeaders.Add("User-Agent", "StormSafe/1.0");

                    var response = await client.GetAsync(standardUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var imageBytes = await response.Content.ReadAsByteArrayAsync();
                        return File(imageBytes, "image/gif");
                    }

                    _logger.LogWarning("Failed to fetch standard radar image for station {Station}", station);
                    return NotFound();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching standard radar image for station {Station}", station);
                    return StatusCode(500, "Error fetching radar image");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetRadarImage for station {Station}, product {Product}", station, product);
                return StatusCode(500, "Error processing radar image request");
            }
        }

        [HttpGet("radar-stations")]
        public async Task<ActionResult<Models.RadarStationsResponse>> GetRadarStations()
        {
            try
            {
                var stations = await _weatherService.GetRadarStationsAsync();
                return Ok(stations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching radar stations");
                return StatusCode(500, "Failed to fetch radar stations");
            }
        }
    }
}