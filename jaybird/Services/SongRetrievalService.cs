namespace jaybird.Services;

using Models;
using System.Net.Http.Headers;
using System.Net;
using System.Net.Http.Json;

public class SongRetrievalService(AppConfig config) : ISongRetrievalService
{
    private readonly HttpClient _httpClient = new();
    private EntityTagHeaderValue? _etag;

    public async Task<SongData?> GetCurrentSongAsync(Station currentStation)
    {
        var apiConfig = GetApiConfig(currentStation);
        var endpoint = $"{apiConfig.BaseUrl}{apiConfig.PlaysEndpoint}";

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            if (_etag != null)
            {
                request.Headers.IfNoneMatch.Add(_etag);
            }

            Utils.DebugLogger.Log($"Fetching song data from: {endpoint}", "SongRetrievalService");
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _etag = response.Headers.ETag;

                var nowPlayingResponse = await response.Content.ReadFromJsonAsync<NowPlayingResponse>();

                if (nowPlayingResponse?.now.recording != null) // check if there's currently a song
                {
                    var recording = nowPlayingResponse.now.recording;
                    var release = recording.Releases.FirstOrDefault();

                    // Extract artwork URL - prefer 340x340 size for medium detail
                    string? artworkUrl = null;
                    if (release?.Artwork != null && release.Artwork.Count > 0)
                    {
                        var artwork = release.Artwork[0];
                        // Try to get 340x340 size, fallback to original URL
                        artworkUrl = artwork.Sizes?.FirstOrDefault(s => s.Width == 340)?.Url ?? artwork.Url;
                    }

                    var songData = new SongData
                    {
                        Title = recording.Title,
                        Artist = recording.Artists.FirstOrDefault()?.Name ?? "Unknown Artist",
                        Album = release?.Title ?? "Unknown Album",
                        PlayedTime = DateTime.TryParse(nowPlayingResponse.now.PlayedTime, out var playedTime)
                            ? playedTime
                            : DateTime.Now,
                        ArtworkUrl = artworkUrl
                    };
                    Utils.DebugLogger.Log($"Song retrieved: {songData.Title} by {songData.Artist} (artwork: {artworkUrl != null})", "SongRetrievalService");
                    return songData;
                }
                {
                    // placeholder
                    Utils.DebugLogger.Log($"No recording data available, showing station name", "SongRetrievalService");
                    return new SongData
                    {
                        Title = "Tuned into: " + currentStation,
                        Artist = "",
                        Album = "",
                        PlayedTime = DateTime.Now
                    };
                }
            }
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                // The song has not changed since the last request
                Utils.DebugLogger.Log("Song data not modified (HTTP 304)", "SongRetrievalService");
                return null;
            }
            {
                Utils.DebugLogger.Log($"Error fetching song data: {response.StatusCode}", "SongRetrievalService");
                throw new HttpRequestException($"Error fetching song data: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            Utils.DebugLogger.LogException(ex, "SongRetrievalService.GetCurrentSongAsync");
            return new SongData
            {
                Title = "Error",
                Artist = "Error",
                Album = "Error",
                PlayedTime = DateTime.Now
            };
        }
    }
    
    private ApiConfig GetApiConfig(Station currentStation)
    {
        return currentStation switch
        {
            Station.TripleJ => config.TripleJApi,
            Station.DoubleJ => config.DoubleJApi,
            Station.Unearthed => config.UnearthedApi,
            _ => throw new ArgumentOutOfRangeException(nameof(currentStation), $"Not expected station value: {currentStation}"),
        };
    }
}