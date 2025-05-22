using System.Collections.Generic;

namespace overlayc
{
    public class Command
    {
        public string template { get; set; } = null!;
        public string label { get; set; } = null!;
        public string description { get; set; } = null!;
        public List<string> @params { get; set; } = new();
        public Dictionary<string, List<string>> options { get; set; } = new();
    }
}
