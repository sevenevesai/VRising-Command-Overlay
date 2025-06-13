using System;

namespace overlayc
{
    public class SettingsData
    {
        public double? WindowLeft     { get; set; }
        public double? WindowTop      { get; set; }
        public bool   HorizontalMode  { get; set; }
        public bool   InvertButtons   { get; set; }

        // Which commands preset file is active
        public string? CommandPreset { get; set; }

        // Templates of commands marked as favorites
        public HashSet<string> Favorites { get; set; } = new();
    }
}
