using jaybird.Models;
using Spectre.Console.Rendering;

namespace jaybird.Tests.TestInfrastructure.Helpers;

/// <summary>
/// Helper class for creating test data objects
/// </summary>
public static class TestDataHelper
{
    /// <summary>
    /// Creates a sample SongData object for testing
    /// </summary>
    public static SongData CreateSampleSongData(
        string artist = "Test Artist",
        string title = "Test Song",
        string album = "Test Album",
        string? artworkUrl = null,
        bool isAustralian = true)
    {
        return new SongData
        {
            Artist = artist,
            Title = title,
            Album = album,
            ArtworkUrl = artworkUrl ?? "https://example.com/artwork.jpg",
            PlayedTime = DateTime.UtcNow,
            IsAustralian = isAustralian
        };
    }

    /// <summary>
    /// Creates a sample RegionalSongData object for testing
    /// </summary>
    public static RegionalSongData CreateSampleRegionalSongData(
        Region region = Region.NSW,
        string artist = "Test Artist",
        string title = "Test Song",
        string album = "Test Album")
    {
        return new RegionalSongData
        {
            Region = region,
            Artist = artist,
            Title = title,
            Album = album,
            ArtworkUrl = "https://example.com/artwork.jpg",
            PlayedTime = DateTime.UtcNow,
            IsAustralian = true
        };
    }

    /// <summary>
    /// Creates a sample UserSettings object for testing
    /// </summary>
    public static UserSettings CreateSampleUserSettings(
        Station lastStation = Station.TripleJ,
        int lastVolume = 50)
    {
        return new UserSettings
        {
            LastStation = lastStation,
            LastVolume = lastVolume,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a mock IRenderable for testing
    /// </summary>
    public static IRenderable CreateMockRenderable()
    {
        var mock = new Mock<IRenderable>();
        mock.Setup(x => x.Measure(It.IsAny<RenderOptions>(), It.IsAny<int>()))
            .Returns(new Measurement(10, 10));
        return mock.Object;
    }

    /// <summary>
    /// Creates a list of sample SongData objects
    /// </summary>
    public static List<SongData> CreateSampleSongDataList(int count = 3)
    {
        var songs = new List<SongData>();
        for (int i = 1; i <= count; i++)
        {
            songs.Add(CreateSampleSongData(
                artist: $"Artist {i}",
                title: $"Song {i}",
                album: $"Album {i}",
                artworkUrl: $"https://example.com/artwork{i}.jpg"
            ));
        }
        return songs;
    }

    /// <summary>
    /// Creates sample API response JSON for testing
    /// </summary>
    public static string CreateSampleApiResponseJson()
    {
        return @"{
            ""items"": [
                {
                    ""recording"": {
                        ""title"": ""Test Song"",
                        ""artists"": [
                            {
                                ""name"": ""Test Artist"",
                                ""is_australian"": true
                            }
                        ],
                        ""releases"": [
                            {
                                ""title"": ""Test Album""
                            }
                        ]
                    },
                    ""played_time"": ""2024-01-01T12:00:00Z""
                }
            ]
        }";
    }
}