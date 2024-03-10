namespace jaybird.Services;

using Models;
using Newtonsoft.Json;

public class SongRetrievalService(AppConfig config) : ISongRetrievalService
{
    private readonly HttpClient _httpClient = new();

    public async Task<SongData> GetCurrentSongAsync(Station currentStation)
    {
        var apiConfig = GetApiConfig(currentStation);
        var response = await _httpClient.GetStringAsync($"{apiConfig.BaseUrl}{apiConfig.PlaysEndpoint}");
        var apiResponse = JsonConvert.DeserializeObject<SongApiResponse>(response);

        if (apiResponse?.Items == null || apiResponse.Items.Count == 0) throw new Exception("No song data available.");

        var firstItem = apiResponse.Items[0];
        var songData = new SongData
        {
            Title = firstItem.Recording.Title,
            Artist = firstItem.Recording.Artists[0].Name,
            Album = firstItem.Recording.Releases[0].Title,
            PlayedTime = Convert.ToDateTime(firstItem.PlayedTime)
        };

        return songData;
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