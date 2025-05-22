using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace overlayc
{
    public static class CommandLoader
    {
        private static readonly JsonSerializer _serializer = new JsonSerializer();

        public static Dictionary<string, Dictionary<string, List<Command>>> LoadCommands(string path)
        {
            using var stream = File.OpenRead(path);
            using var sr     = new StreamReader(stream);
            using var reader = new JsonTextReader(sr);

            var data = _serializer
                .Deserialize<Dictionary<string, Dictionary<string, List<Command>>>>(reader);

            return data ?? new();
        }
    }
}
