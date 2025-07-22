using System;
using System.IO;
using System.Text.Json;

namespace ResxTranslator
{
    public class AwsConfigManager
    {
        private const string ConfigFileName = "aws-config.json";
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

        public class AwsConfig
        {
            public string ProfileName { get; set; } = "default";
            public string Region { get; set; } = "us-east-1";
        }

        public static AwsConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var json = File.ReadAllText(ConfigFilePath);
                    return JsonSerializer.Deserialize<AwsConfig>(json) ?? new AwsConfig();
                }
            }
            catch (Exception)
            {
                // If config file is corrupted or unreadable, return default config
            }

            return new AwsConfig();
        }

        public static void SaveConfig(AwsConfig config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception)
            {
                // Silently fail if we can't save config - app can still function
            }
        }

        public static void SaveConfig(string profileName, string region = "us-east-1")
        {
            SaveConfig(new AwsConfig { ProfileName = profileName, Region = region });
        }
    }
}
