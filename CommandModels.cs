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

        // Indicates if this command is marked as a favorite by the user
        public bool isStarred { get; set; }

        // Label shown in the UI includes a star prefix when starred
        public string displayLabel => (isStarred ? "★ " : "") + label;
    }
}