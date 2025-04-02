using Microsoft.AspNetCore.Mvc;
using StormSafe.Services;
using StormSafe.Models;

namespace StormSafe.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly IWeatherService _weatherService;

        public WeatherController(IWeatherService weatherService)
        {
            _weatherService = weatherService;
        }

        [HttpGet("storm-data")]
        public async Task<ActionResult<StormData>> GetStormData([FromQuery] double latitude, [FromQuery] double longitude)
        {
            try
            {
                var stormData = await _weatherService.GetStormDataAsync(latitude, longitude);
                return Ok(stormData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to fetch storm data", details = ex.Message });
            }
        }

        [HttpGet("radar-image")]
        public ActionResult<string> GetRadarImage([FromQuery] double latitude, [FromQuery] double longitude)
        {
            try
            {
                var radarUrl = _weatherService.GetRadarImageUrl(latitude, longitude);
                return Ok(new { url = radarUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to fetch radar image URL", details = ex.Message });
            }
        }
    }
}