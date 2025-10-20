namespace jaybird.Models;

using System.Text.Json.Serialization;

public class SongData
{
    public required string Artist { get; set; }
    public DateTime PlayedTime { get; set; }
    public required string Title { get; set; }
    public required string Album { get; set; }
    public string? ArtworkUrl { get; set; }
    public bool IsAustralian { get; set; }
}

public class SongApiResponse
{
    [JsonPropertyName("items")] public required List<PlayItem> Items { get; set; } = new();
}

public class PlayItem
{
    public required Recording Recording { get; set; }
    [JsonPropertyName("played_time")] public required string PlayedTime { get; set; }
}

public class Recording
{
    public required string Title { get; set; }
    public required List<Artist> Artists { get; set; } = new();
    public required List<Release> Releases { get; set; } = new();
}

public class Artist
{
    public required string Name { get; set; }
    public List<Artwork>? Artwork { get; set; }
    [JsonPropertyName("is_australian")]
    public bool? IsAustralian { get; set; }
}

public class Release
{
    public required string Title { get; set; }
    public List<Artwork>? Artwork { get; set; }
}

public class Artwork
{
    public required string Url { get; set; }
    public List<ArtworkSize>? Sizes { get; set; }
}

public class ArtworkSize
{
    public required string Url { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class NowPlayingResponse // Example - to match API structure
{
    public NowPlayingItem? now { get; set; }
}

public class NowPlayingItem
{
    public Recording? recording { get; set; }
    [JsonPropertyName("played_time")]
    public string? PlayedTime { get; set; }
}