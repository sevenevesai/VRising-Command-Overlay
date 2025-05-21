using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace overlayc
{
    public static class CommandLoader
    {
        public static Dictionary<string, Dictionary<string, List<Command>>> LoadCommands(string path)
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<Command>>>>(json)
                   ?? new();
        }
    }
}
