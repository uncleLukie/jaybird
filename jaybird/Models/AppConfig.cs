namespace jaybird.Models;
public class AppConfig
{ 
        public required ApiConfig TripleJApi { get; set; }
        public required ApiConfig DoubleJApi { get; set; }
        public required ApiConfig UnearthedApi { get; set; }
        public required AudioConfig Audio { get; set; }
        public required DiscordConfig Discord { get; set; }
}