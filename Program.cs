using StormSafe.Services;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure HTTP client for NOAA API
builder.Services.AddHttpClient("NOAA", client =>
{
    client.BaseAddress = new Uri("https://api.weather.gov/");
    client.DefaultRequestHeaders.Add("User-Agent", "StormSafe/1.0");
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/geo+json"));
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register WeatherService with the NOAA HttpClient
builder.Services.AddScoped<IWeatherService>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<WeatherService>>();
    var httpClient = httpClientFactory.CreateClient("NOAA");
    return new WeatherService(httpClient, logger);
});

builder.Services.AddMemoryCache();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Configure HTTPS port
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5202); // HTTP port
    options.ListenAnyIP(7202, listenOptions => // HTTPS port
    {
        listenOptions.UseHttps();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Enable CORS
app.UseCors("AllowAll");

app.UseAuthorization();

// Map API routes first
app.MapControllers();

// Configure default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
