using Microsoft.AspNetCore.Mvc;
using StormSafe.Services;
using StormSafe.Models;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace StormSafe.Controllers
{
    /// <summary>
    /// Controller for handling weather-related API endpoints
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class WeatherController : ControllerBase
    {
        private readonly IWeatherService _weatherService;
        private readonly ILogger<WeatherController> _logger;

        public WeatherController(
            IWeatherService weatherService,
            ILogger<WeatherController> logger)
        {
            _weatherService = weatherService;
            _logger = logger;
        }

        /// <summary>
        /// Gets storm data for a specific location
        /// </summary>
        /// <param name="latitude">The latitude coordinate (e.g., 40.7128 for New York)</param>
        /// <param name="longitude">The longitude coordinate (e.g., -74.0060 for New York)</param>
        /// <returns>Storm data including current conditions and predictions</returns>
        /// <response code="200">Returns the storm data</response>
        /// <response code="400">If the latitude or longitude parameters are missing or invalid</response>
        /// <response code="404">If no valid weather data is found for the specified location</response>
        /// <response code="500">If there was an error fetching the data</response>
        [HttpGet("storm-data")]
        [ProducesResponseType(typeof(StormData), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<StormData>> GetStormData(
            [FromQuery, Required] double latitude,
            [FromQuery, Required] double longitude)
        {
            try
            {
                // Round coordinates to 4 decimal places
                latitude = Math.Round(latitude, 4);
                longitude = Math.Round(longitude, 4);

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
            catch (Exception ex) when (ex.Message.Contains("No valid weather data found within"))
            {
                _logger.LogWarning("No valid weather data found within search radius for coordinates: lat={Latitude}, lon={Longitude}", latitude, longitude);
                return NotFound(new { error = "No weather stations with valid data found within 31 miles of the specified location" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching storm data for coordinates: lat={Latitude}, lon={Longitude}. Error: {Error}",
                    latitude, longitude, ex.Message);

                // In development, return detailed error information
                if (HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() ?? false)
                {
                    return StatusCode(500, new
                    {
                        error = "An unexpected error occurred while fetching storm data",
                        details = ex.ToString(),
                        innerException = ex.InnerException?.ToString(),
                        stackTrace = ex.StackTrace
                    });
                }

                return StatusCode(500, new { error = "An unexpected error occurred while fetching storm data" });
            }
        }

        /// <summary>
        /// Gets the radar image URL for a specific location
        /// </summary>
        /// <param name="latitude">The latitude coordinate (e.g., 40.7128 for New York)</param>
        /// <param name="longitude">The longitude coordinate (e.g., -74.0060 for New York)</param>
        /// <param name="zoom">The zoom level of the map (default: 6)</param>
        /// <returns>The URL of the radar image</returns>
        /// <response code="200">Returns the radar image URL</response>
        /// <response code="500">If there was an error fetching the URL</response>
        [HttpGet("radar-image")]
        [ProducesResponseType(typeof(RadarImageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<RadarImageResponse>> GetRadarImage(
            [FromQuery] double latitude,
            [FromQuery] double longitude,
            [FromQuery] int zoom = 6)
        {
            try
            {
                _logger.LogInformation("Getting radar image for coordinates: lat={Latitude}, lon={Longitude}, zoom={Zoom}",
                    latitude, longitude, zoom);
                var url = await _weatherService.GetRadarImageUrlAsync(latitude, longitude, zoom);
                return Ok(new RadarImageResponse { Url = url });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting radar image for coordinates: lat={Latitude}, lon={Longitude}, zoom={Zoom}",
                    latitude, longitude, zoom);
                return StatusCode(500, new { error = "An unexpected error occurred while fetching radar image" });
            }
        }

        /// <summary>
        /// Gets radar data for a specific station and product
        /// </summary>
        /// <param name="stationId">The radar station ID</param>
        /// <param name="product">The radar product type</param>
        /// <returns>The URL of the radar image</returns>
        /// <response code="200">Returns the radar image URL</response>
        /// <response code="500">If there was an error fetching the URL</response>
        [HttpGet("radar/{stationId}/{product}")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<string> GetRadarByStation(string stationId, string product)
        {
            try
            {
                _logger.LogInformation("Getting radar image for station: {StationId}, product: {Product}", stationId, product);
                var url = $"https://radar.weather.gov/ridge/standard/{stationId}_{product}.png";
                return Ok(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting radar image for station: {StationId}, product: {Product}",
                    stationId, product);
                return StatusCode(500, new { error = "An unexpected error occurred while fetching radar image" });
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

        [HttpGet("stations")]
        [ProducesResponseType(typeof(List<ObservationStationFeature>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllStations()
        {
            try
            {
                var stations = await _weatherService.GetAllStationsAsync();
                return Ok(stations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stations");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving stations");
            }
        }
    }
}