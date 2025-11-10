using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace C.A.S.S.I.E
{
    public static class AppConfigManager
    {
        public static AppConfig Load(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath))
                return new AppConfig();

            if (!File.Exists(configPath))
                return new AppConfig();

            try
            {
                using (var fs = File.OpenRead(configPath))
                {
                    var ser = new DataContractJsonSerializer(typeof(AppConfig));
                    var cfg = ser.ReadObject(fs) as AppConfig;
                    return cfg ?? new AppConfig();
                }
            }
            catch
            {
                return new AppConfig();
            }
        }

        public static void Save(string configPath, AppConfig config)
        {
            if (string.IsNullOrWhiteSpace(configPath) || config == null)
                return;

            try
            {
                var dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using (var fs = File.Create(configPath))
                {
                    var ser = new DataContractJsonSerializer(typeof(AppConfig));
                    ser.WriteObject(fs, config);
                }
            }
            catch
            {
            }
        }
    }
}