# StormSafe - Real-Time Storm Tracking Platform

StormSafe is a web application designed to provide real-time storm tracking and weather information. It helps users monitor approaching storms, track their speed and direction, and estimate arrival times based on their location.

## Features

- **Live Radar Display**: Interactive map showing current weather conditions
- **Storm Tracking**: Real-time monitoring of storm movement and intensity
- **Location-Based Alerts**: Personalized storm information based on user location
- **Storm Information Dashboard**:
  - Storm Speed
  - Direction
  - Estimated Arrival Time
  - Distance to User
  - Storm Intensity

## Technology Stack

- ASP.NET Core MVC
- OpenStreetMap with Leaflet.js for map visualization
- National Weather Service API for weather data
- Bootstrap for responsive design

## Prerequisites

- .NET 10.0 SDK or later
- A modern web browser with JavaScript enabled
- Location services enabled (for personalized weather information)

## Getting Started

1. Clone the repository:

   ```bash
   git clone https://github.com/yourusername/StormSafe.git
   cd StormSafe
   ```

2. Set up your configuration:

   - Copy `appsettings.Example.json` to `appsettings.json`
   - Copy `appsettings.Development.Example.json` to `appsettings.Development.json`
   - Update the API keys in the settings files

3. Run the application:

   ```bash
   dotnet run
   ```

4. Open your browser and navigate to:
   - https://localhost:5202 (or the port shown in your console)

## Configuration

The application requires the following configuration in `appsettings.json`:

```json
{
  "OpenWeatherApi": {
    "ApiKey": "your-api-key-here"
  }
}
```

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- OpenStreetMap for providing map data
- National Weather Service for weather information
- Leaflet.js for the map visualization library
