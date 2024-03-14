namespace jaybird.Services;

using Models;
using System.Net.Http.Json;

public class SongRetrievalService(AppConfig config) : ISongRetrievalService
{
    private readonly HttpClient _httpClient = new();

    public async Task<SongData> GetCurrentSongAsync(Station currentStation)
    {
        var apiConfig = GetApiConfig(currentStation);
        var endpoint = $"{apiConfig.BaseUrl}{apiConfig.PlaysEndpoint}";

        try
        {
            var nowPlayingResponse = await _httpClient.GetFromJsonAsync<NowPlayingResponse>(endpoint);

            if (nowPlayingResponse?.now.recording != null) // check if there's currently a song
            {
                var recording = nowPlayingResponse.now.recording;
                var songData = new SongData
                {
                    Title = recording.Title, 
                    Artist = recording.Artists.FirstOrDefault()?.Name ?? "Unknown Artist",
                    Album = recording.Releases.FirstOrDefault()?.Title ?? "Unknown Album",
                    PlayedTime = DateTime.TryParse(nowPlayingResponse.now.PlayedTime, out var playedTime) 
                        ? playedTime 
                        : DateTime.Now
                };
                return songData;
            }
            else
            {
                // placeholder
                return new SongData
                {
                    Title = "Tuned into: " + currentStation,
                    Artist = "", 
                    Album = "", 
                    PlayedTime = DateTime.Now
                };
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error fetching song data: {ex.Message}");
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