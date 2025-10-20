namespace jaybird.Services;

using Models;
using System.Net.Http.Headers;
using System.Net;
using System.Net.Http.Json;

public class RegionalSongRetrievalService(AppConfig config, TimezoneService timezoneService) : IRegionalSongRetrievalService
{
    private readonly HttpClient _httpClient = new();
    private readonly Dictionary<(Station, Region), EntityTagHeaderValue?> _etags = new();

    public async Task<SongData?> GetCurrentSongAsync(Station currentStation)
    {
        // Default to NSW region for backward compatibility
        return await GetCurrentSongAsync(currentStation, Region.NSW);
    }

    public async Task<RegionalSongData?> GetCurrentSongAsync(Station currentStation, Region region)
    {
        // Use timezone parameters instead of broken regional endpoints
        var baseEndpoint = config.GetApiConfig(currentStation).PlaysEndpoint;
        var timezone = region.GetTimezone();
        var endpoint = $"{config.GetApiConfig(currentStation).BaseUrl}{baseEndpoint}?tz={Uri.EscapeDataString(timezone)}";

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            var key = (currentStation, region);
            if (_etags.TryGetValue(key, out var etag) && etag != null)
            {
                request.Headers.IfNoneMatch.Add(etag);
            }

            Utils.DebugLogger.Log($"Fetching regional song data from: {endpoint} ({region} - {timezone})", "RegionalSongRetrievalService");
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _etags[key] = response.Headers.ETag;

                var nowPlayingResponse = await response.Content.ReadFromJsonAsync<NowPlayingResponse>();

                if (nowPlayingResponse?.now?.recording != null) // check if there's currently a song
                {
                    var recording = nowPlayingResponse.now.recording;
                    var release = recording.Releases?.FirstOrDefault();

                    // Extract full-size artist artwork URL
                    string? artworkUrl = null;
                    var artist = recording.Artists?.FirstOrDefault();
                    if (artist?.Artwork != null && artist.Artwork.Count > 0)
                    {
                        // Use full-size artist artwork URL
                        artworkUrl = artist.Artwork[0].Url;
                    }
                    else if (release?.Artwork != null && release.Artwork.Count > 0)
                    {
                        // Fallback to release artwork if artist artwork not available
                        artworkUrl = release.Artwork[0].Url;
                    }

                    var baseSongData = new SongData
                    {
                        Title = recording.Title ?? "Unknown Title",
                        Artist = artist?.Name ?? "Unknown Artist",
                        Album = release?.Title ?? "Unknown Album",
                        PlayedTime = DateTime.TryParse(nowPlayingResponse.now?.PlayedTime, out var playedTime)
                            ? playedTime
                            : DateTime.Now,
                        ArtworkUrl = artworkUrl
                    };

                    var regionalSongData = timezoneService.ApplyDelayToSongData(baseSongData, region);
                    
                    Utils.DebugLogger.Log($"Regional song retrieved: {regionalSongData.Title} by {regionalSongData.Artist} ({region} - {regionalSongData.GetDelayDisplay()})", "RegionalSongRetrievalService");
                    return regionalSongData;
                }
                else
                {
                    // placeholder
                    Utils.DebugLogger.Log($"No recording data available for {region}, showing station name", "RegionalSongRetrievalService");
                    var baseSongData = new SongData
                    {
                        Title = "Tuned into: " + currentStation,
                        Artist = "",
                        Album = "",
                        PlayedTime = DateTime.Now
                    };
                    
                    return timezoneService.ApplyDelayToSongData(baseSongData, region);
                }
            }
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                // The song has not changed since the last request
                Utils.DebugLogger.Log($"Regional song data not modified for {region} (HTTP 304)", "RegionalSongRetrievalService");
                return null;
            }
            else
            {
                Utils.DebugLogger.Log($"Error fetching regional song data for {region}: {response.StatusCode}", "RegionalSongRetrievalService");
                throw new HttpRequestException($"Error fetching regional song data for {region}: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            Utils.DebugLogger.LogException(ex, $"RegionalSongRetrievalService.GetCurrentSongAsync({region})");
            var baseSongData = new SongData
            {
                Title = "Error",
                Artist = "Error",
                Album = "Error",
                PlayedTime = DateTime.Now
            };
            
            return timezoneService.ApplyDelayToSongData(baseSongData, region);
        }
    }
    
    public async Task<Dictionary<Region, RegionalSongData?>> GetAllRegionalSongsAsync(Station currentStation)
    {
        var tasks = Enum.GetValues<Region>().Select(region => GetCurrentSongAsync(currentStation, region));
        var results = await Task.WhenAll(tasks);
        
        var regionalSongs = new Dictionary<Region, RegionalSongData?>();
        var regions = Enum.GetValues<Region>();
        
        for (int i = 0; i < regions.Length; i++)
        {
            regionalSongs[regions[i]] = results[i];
        }
        
        return regionalSongs;
    }
    
    public void ClearEtags()
    {
        _etags.Clear();
    }
    
    public void ClearEtagForRegion(Station station, Region region)
    {
        _etags.Remove((station, region));
    }
}