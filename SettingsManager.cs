using System;
using System.IO;
using System.Text.Json;

namespace overlayc
{
    public static class SettingsManager
    {
        public static SettingsData? Load(string fileName)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SettingsData>(json);
        }

        public static void Save(string fileName, SettingsData settings)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
    }
}
