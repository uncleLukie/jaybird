namespace jaybird.Models;

using System.Text.Json.Serialization;

public class SongData
{
    public string Artist { get; set; }
    public DateTime PlayedTime { get; set; }
    public string Title { get; set; }
    public string Album { get; set; }
}

public class SongApiResponse
{
    [JsonPropertyName("items")] public List<PlayItem> Items { get; set; }
}

public class PlayItem
{
    public Recording Recording { get; set; }
    [JsonPropertyName("played_time")] public string PlayedTime { get; set; }
}

public class Recording
{
    public string Title { get; set; }
    public List<Artist> Artists { get; set; }
    public List<Release> Releases { get; set; }
}

public class Artist
{
    public string Name { get; set; }
}

public class Release
{
    public string Title { get; set; }
}

public class NowPlayingResponse // Example - to match API structure
{
    public NowPlayingItem now { get; set; } 
}

public class NowPlayingItem
{
    public Recording recording { get; set; }
    [JsonPropertyName("played_time")]
    public string PlayedTime { get; set; }
}