using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace StormSafe.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RadarController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RadarController> _logger;

        public RadarController(IHttpClientFactory httpClientFactory, ILogger<RadarController> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "StormSafe/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        [HttpGet("proxy")]
        public async Task<IActionResult> Proxy([FromQuery] string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                _logger.LogWarning("Proxy request received with empty URL");
                return BadRequest("URL parameter is required");
            }

            try
            {
                _logger.LogInformation($"Fetching radar image from URL: {url}");

                // Add specific headers for NOAA API
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "StormSafe/1.0 (https://github.com/yourusername/StormSafe)");
                request.Headers.Add("Accept", "image/gif, image/png, image/jpeg");
                request.Headers.Add("Referer", "https://radar.weather.gov/");

                var response = await _httpClient.SendAsync(request);

                _logger.LogInformation($"Response status code: {response.StatusCode}");
                _logger.LogInformation($"Response headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}"))}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Failed to fetch radar image. Status code: {response.StatusCode}, URL: {url}, Error: {errorContent}");

                    // If we get a 404, try the next URL format
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return NotFound($"Radar image not found at URL: {url}");
                    }

                    return StatusCode((int)response.StatusCode, $"Failed to fetch radar image: {errorContent}");
                }

                var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/gif";
                var content = await response.Content.ReadAsByteArrayAsync();

                if (content.Length == 0)
                {
                    _logger.LogWarning($"Received empty content from radar image URL: {url}");
                    return StatusCode(502, "Received empty content from radar server");
                }

                _logger.LogInformation($"Successfully fetched radar image from URL: {url}, Content length: {content.Length} bytes");

                // Set response headers
                Response.Headers.Append("Access-Control-Allow-Origin", "*");
                Response.Headers.Append("Access-Control-Allow-Methods", "GET");
                Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                Response.Headers.Append("Pragma", "no-cache");
                Response.Headers.Append("Expires", "0");
                Response.Headers.Append("Content-Type", contentType);

                // Return the image content
                return new FileContentResult(content, contentType);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"HTTP request failed for URL: {url}");
                return StatusCode(502, $"Error fetching radar image: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, $"Request timed out for URL: {url}");
                return StatusCode(504, "Request timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error while fetching radar image from URL: {url}");
                return StatusCode(500, $"Error fetching radar image: {ex.Message}");
            }
        }
    }
}