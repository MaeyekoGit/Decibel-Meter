using System.IO;
using DecibelMeter.Models;
using Newtonsoft.Json;

namespace DecibelMeter
{
    public static class ConfigService
    {
        private static readonly string ConfigPath = "appsettings.json";
        public static Config Load()
        {
            if (!File.Exists(ConfigPath))
                return new Config();

            string json = File.ReadAllText(ConfigPath);
            return JsonConvert.DeserializeObject<Config>(json) ?? new Config();
        }

        public static void Save(Config config)
        {
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }
    }
}
