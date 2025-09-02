using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DecibelMeter.Models
{
    public class Config
    {
        public string SelectedDevice { get; set; } = "";
        public int ThresholdDb { get; set; } = 50;
        public int SelectedMonitor { get; set; } = 0;
        public string LastWarningSoundPath { get; set; } = "notificationsound.wav";
        public int WarningSoundVolume { get; set; } = 100;
    }
}
