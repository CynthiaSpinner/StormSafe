using System;

namespace StormSafe.Models
{
    public class StormData
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Speed { get; set; } // Speed in mph
        public double Direction { get; set; } // Direction in degrees
        public double Intensity { get; set; } // Storm intensity (0-100)
        public DateTime EstimatedArrivalTime { get; set; }
        public double DistanceToUser { get; set; } // Distance in miles
        public string StormType { get; set; }
        public string RadarImageUrl { get; set; }
    }
}