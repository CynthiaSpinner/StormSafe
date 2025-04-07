# StormSafe

StormSafe is a real-time storm tracking and weather monitoring application that provides users with up-to-date information about weather conditions, storm movements, and potential hazards in their area. The application continuously updates weather data to provide precise storm tracking and accurate arrival time predictions.

## Features

- **Real-time Weather Data**: Get current weather conditions from nearby NOAA weather stations
- **Storm Tracking**: Monitor storm movements and intensities
- **Interactive Map**: View weather patterns and storm paths on an interactive map
- **Weather Alerts**: Receive notifications about severe weather conditions
- **Local Weather Information**: Access detailed weather data for your location
- **NEXRAD Radar**: View real-time radar data from the National Weather Service
- **Storm Arrival Predictions**: Get accurate estimates of when storms will reach your location
- **Continuous Updates**: Data refreshes automatically to provide the most current storm information

## Future Features

- **Precise Storm Tracking**: Real-time monitoring of storm paths and movements
- **Arrival Time Calculations**: Accurate predictions of when storms will reach specific locations
- **Location-Based Alerts**: Personalized notifications based on your exact location
- **Storm Impact Analysis**: Detailed information about potential storm effects in your area
- **Historical Storm Data**: Access to past storm patterns and behaviors

## Technologies Used

- **Backend**: ASP.NET Core
- **Frontend**: HTML, CSS, JavaScript, Leaflet.js
- **APIs**:
  - NOAA Weather API for real-time weather data
  - Iowa Environmental Mesonet for NEXRAD radar data
  - OpenStreetMap for base map data

## Getting Started

### Prerequisites

- .NET 6.0 SDK or later
- A modern web browser
- Internet connection for API access

### Installation

1. Clone the repository:

   ```bash
   git clone https://github.com/yourusername/StormSafe.git
   ```

2. Navigate to the project directory:

   ```bash
   cd StormSafe
   ```

3. Restore dependencies:

   ```bash
   dotnet restore
   ```

4. Build the project:

   ```bash
   dotnet build
   ```

5. Run the application:

   ```bash
   dotnet run
   ```

6. Open your browser and navigate to `https://localhost:5001` or `http://localhost:5000`

## Usage

1. **View Current Weather**:

   - Click "Use My Location" to get weather data for your current location
   - View temperature, wind speed, and other weather conditions

2. **Track Storms**:

   - Use the interactive map to view storm locations
   - Click "Show NEXRAD" to view radar data
   - Adjust radar opacity and product type using the controls
   - Monitor storm movement and predicted paths

3. **Monitor Alerts**:
   - View active weather alerts in your area
   - Get detailed information about storm types and intensities
   - Receive updates about storm arrival times

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- NOAA for providing weather data and APIs
- Iowa Environmental Mesonet for NEXRAD radar data
- OpenStreetMap for base map data
- Leaflet.js for the interactive map functionality
