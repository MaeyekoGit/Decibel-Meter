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

        public int WarningSoundVolume { get; set; } = 100;

        // Paths
        public string LastWarningSoundPath { get; set; } = "Assets/default_notificationsound.wav";
        public string OverlayImagePath { get; set; } = "Assets/default_overlay.png";

        // Feature toggles
        public bool EnableWarningSound { get; set; } = true;
        public bool EnableOverlay { get; set; } = true;
    }
}
