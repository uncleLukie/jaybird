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

        if (apiResponse?.Items == null || !apiResponse.Items.Any())
        {
            Console.WriteLine("No song data available or API response is empty.");
            return new SongData
            {
                Title = "Unknown",
                Artist = "Unknown",
                Album = "Unknown",
                PlayedTime = DateTime.Now
            };
        }

        var firstItem = apiResponse.Items.FirstOrDefault();
        if (firstItem == null || firstItem.Recording == null || !firstItem.Recording.Artists.Any() || !firstItem.Recording.Releases.Any())
        {
            Console.WriteLine("Incomplete song data.");
            return new SongData
            {
                Title = "Incomplete Data",
                Artist = "Incomplete Data",
                Album = "Incomplete Data",
                PlayedTime = DateTime.Now 
            };
        }

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