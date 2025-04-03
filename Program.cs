using StormSafe.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IWeatherService, WeatherService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("WeatherAPI", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "StormSafe/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Configure HTTP client with custom User-Agent for NOAA API
builder.Services.AddHttpClient("NOAA", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "StormSafe/1.0 (https://github.com/yourusername/StormSafe; your.email@example.com)");
    client.Timeout = TimeSpan.FromSeconds(30);
});

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

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "StormSafe API", Version = "v1" });
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
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "StormSafe API V1");
    });
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

// Configure API routes
app.MapControllerRoute(
    name: "api",
    pattern: "api/{controller}/{action}/{id?}");

// Then map MVC routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
