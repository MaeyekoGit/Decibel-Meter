using System;
using System.Text.Json.Serialization;

namespace DecibelMeter.Models
{
    public class Config
    {
        public string SelectedDevice { get; set; } = "";

        // Stored/used as percentage (0-100). Keep old JSON field name "ThresholdDb" for migration.
        [JsonPropertyName("ThresholdDb")]
        public int ThresholdPercent { get; set; } = 50;

        public int SelectedMonitor { get; set; } = 0;
        public string LastWarningSoundPath { get; set; } = "notificationsound.wav";
        public int WarningSoundVolume { get; set; } = 100;
    }
}
