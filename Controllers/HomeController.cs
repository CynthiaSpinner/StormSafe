using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using StormSafe.Services;
using Stormsafe.Models;

namespace Stormsafe.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IWeatherService _weatherService;
    private readonly IConfiguration _configuration;

    public HomeController(ILogger<HomeController> logger, IWeatherService weatherService, IConfiguration configuration)
    {
        _logger = logger;
        _weatherService = weatherService;
        _configuration = configuration;
    }

    public IActionResult Index()
    {
        ViewBag.OpenWeatherMapApiKey = _configuration["WeatherApi:OpenWeatherMapApiKey"];
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
