using Microsoft.AspNetCore.Mvc;
using StormSafe.Services;
using StormSafe.Models;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

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

        public WeatherController(IWeatherService weatherService, ILogger<WeatherController> logger)
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
                _logger.LogInformation($"Fetching storm data for coordinates: lat={latitude}, lon={longitude}");

                var stormData = await _weatherService.GetStormDataAsync(latitude, longitude);

                _logger.LogInformation($"Successfully retrieved storm data: {System.Text.Json.JsonSerializer.Serialize(stormData)}");
                return Ok(stormData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching storm data for coordinates: lat={latitude}, lon={longitude}");
                return StatusCode(500, new
                {
                    error = "Failed to fetch storm data",
                    details = ex.Message,
                    stackTrace = ex.StackTrace
                });
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
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<string> GetRadarImage(
            [FromQuery, Required] double latitude,
            [FromQuery, Required] double longitude)
        {
            try
            {
                _logger.LogInformation($"Fetching radar image URL for coordinates: lat={latitude}, lon={longitude}");

                var radarUrl = _weatherService.GetRadarImageUrl(latitude, longitude);

                _logger.LogInformation($"Successfully retrieved radar URL: {radarUrl}");
                return Ok(new { url = radarUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching radar image URL for coordinates: lat={latitude}, lon={longitude}");
                return StatusCode(500, new
                {
                    error = "Failed to fetch radar image URL",
                    details = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
    }
}