using jaybird.Models;
using System.Text.Json;

namespace jaybird.Tests.Unit.Models;

/// <summary>
/// Unit tests for SongData and related models
/// </summary>
public class SongDataTests
{
    [Fact]
    public void SongData_WithValidData_CreatesCorrectly()
    {
        // Arrange
        var artist = "Test Artist";
        var title = "Test Song";
        var album = "Test Album";
        var artworkUrl = "https://example.com/artwork.jpg";
        var playedTime = DateTime.UtcNow;
        var isAustralian = true;

        // Act
        var songData = new SongData
        {
            Artist = artist,
            Title = title,
            Album = album,
            ArtworkUrl = artworkUrl,
            PlayedTime = playedTime,
            IsAustralian = isAustralian
        };

        // Assert
        songData.Artist.Should().Be(artist);
        songData.Title.Should().Be(title);
        songData.Album.Should().Be(album);
        songData.ArtworkUrl.Should().Be(artworkUrl);
        songData.PlayedTime.Should().Be(playedTime);
        songData.IsAustralian.Should().Be(isAustralian);
    }

    [Fact]
    public void RegionalSongData_WithValidData_CreatesCorrectly()
    {
        // Arrange
        var region = Region.NSW;
        var artist = "Test Artist";
        var title = "Test Song";
        var album = "Test Album";
        var artworkUrl = "https://example.com/artwork.jpg";
        var playedTime = DateTime.UtcNow;
        var isAustralian = true;

        // Act
        var regionalSongData = new RegionalSongData
        {
            Region = region,
            Artist = artist,
            Title = title,
            Album = album,
            ArtworkUrl = artworkUrl,
            PlayedTime = playedTime,
            IsAustralian = isAustralian
        };

        // Assert
        regionalSongData.Region.Should().Be(region);
        regionalSongData.Artist.Should().Be(artist);
        regionalSongData.Title.Should().Be(title);
        regionalSongData.Album.Should().Be(album);
        regionalSongData.ArtworkUrl.Should().Be(artworkUrl);
        regionalSongData.PlayedTime.Should().Be(playedTime);
        regionalSongData.IsAustralian.Should().Be(isAustralian);
    }

    [Fact]
    public void SongApiResponse_WithValidData_CreatesCorrectly()
    {
        // Arrange
        var items = new List<PlayItem>
        {
            new PlayItem
            {
                Recording = new Recording
                {
                    Title = "Test Song",
                    Artists = new List<Artist>
                    {
                        new Artist { Name = "Test Artist", IsAustralian = true }
                    },
                    Releases = new List<Release>
                    {
                        new Release { Title = "Test Album" }
                    }
                },
                PlayedTime = "2024-01-01T12:00:00Z"
            }
        };

        // Act
        var response = new SongApiResponse
        {
            Items = items
        };

        // Assert
        response.Items.Should().HaveCount(1);
        response.Items[0].Recording.Title.Should().Be("Test Song");
        response.Items[0].Recording.Artists[0].Name.Should().Be("Test Artist");
        response.Items[0].Recording.Releases[0].Title.Should().Be("Test Album");
    }

    [Fact]
    public void Artist_WithValidData_CreatesCorrectly()
    {
        // Arrange
        var name = "Test Artist";
        var isAustralian = true;
        var artwork = new List<Artwork>
        {
            new Artwork
            {
                Url = "https://example.com/artwork.jpg",
                Sizes = new List<ArtworkSize>
                {
                    new ArtworkSize { Url = "https://example.com/small.jpg", Width = 100, Height = 100 }
                }
            }
        };

        // Act
        var artist = new Artist
        {
            Name = name,
            IsAustralian = isAustralian,
            Artwork = artwork
        };

        // Assert
        artist.Name.Should().Be(name);
        artist.IsAustralian.Should().Be(isAustralian);
        artist.Artwork.Should().HaveCount(1);
        artist.Artwork[0].Url.Should().Be("https://example.com/artwork.jpg");
    }

    [Fact]
    public void Release_WithValidData_CreatesCorrectly()
    {
        // Arrange
        var title = "Test Album";
        var artwork = new List<Artwork>
        {
            new Artwork
            {
                Url = "https://example.com/album-art.jpg",
                Sizes = new List<ArtworkSize>
                {
                    new ArtworkSize { Url = "https://example.com/album-small.jpg", Width = 200, Height = 200 }
                }
            }
        };

        // Act
        var release = new Release
        {
            Title = title,
            Artwork = artwork
        };

        // Assert
        release.Title.Should().Be(title);
        release.Artwork.Should().HaveCount(1);
        release.Artwork[0].Url.Should().Be("https://example.com/album-art.jpg");
    }

    [Fact]
    public void Artwork_WithValidData_CreatesCorrectly()
    {
        // Arrange
        var url = "https://example.com/artwork.jpg";
        var sizes = new List<ArtworkSize>
        {
            new ArtworkSize { Url = "https://example.com/small.jpg", Width = 100, Height = 100 },
            new ArtworkSize { Url = "https://example.com/large.jpg", Width = 500, Height = 500 }
        };

        // Act
        var artwork = new Artwork
        {
            Url = url,
            Sizes = sizes
        };

        // Assert
        artwork.Url.Should().Be(url);
        artwork.Sizes.Should().HaveCount(2);
        artwork.Sizes[0].Width.Should().Be(100);
        artwork.Sizes[1].Width.Should().Be(500);
    }

    [Fact]
    public void ArtworkSize_WithValidData_CreatesCorrectly()
    {
        // Arrange
        var url = "https://example.com/artwork.jpg";
        var width = 300;
        var height = 300;

        // Act
        var artworkSize = new ArtworkSize
        {
            Url = url,
            Width = width,
            Height = height
        };

        // Assert
        artworkSize.Url.Should().Be(url);
        artworkSize.Width.Should().Be(width);
        artworkSize.Height.Should().Be(height);
    }

    [Fact]
    public void SongData_SerializesToJson_Correctly()
    {
        // Arrange
        var songData = new SongData
        {
            Artist = "Test Artist",
            Title = "Test Song",
            Album = "Test Album",
            ArtworkUrl = "https://example.com/artwork.jpg",
            PlayedTime = DateTime.UtcNow,
            IsAustralian = true
        };

        // Act
        var json = JsonSerializer.Serialize(songData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        json.Should().Contain("test artist");
        json.Should().Contain("test song");
        json.Should().Contain("test album");
        json.Should().Contain("artwork.jpg");
        json.Should().Contain("true");
    }

    [Fact]
    public void SongData_DeserializesFromJson_Correctly()
    {
        // Arrange
        var json = @"{
            ""artist"": ""Test Artist"",
            ""title"": ""Test Song"",
            ""album"": ""Test Album"",
            ""artworkUrl"": ""https://example.com/artwork.jpg"",
            ""playedTime"": ""2024-01-01T12:00:00Z"",
            ""isAustralian"": true
        }";

        // Act
        var songData = JsonSerializer.Deserialize<SongData>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        songData.Should().NotBeNull();
        songData!.Artist.Should().Be("Test Artist");
        songData.Title.Should().Be("Test Song");
        songData.Album.Should().Be("Test Album");
        songData.ArtworkUrl.Should().Be("https://example.com/artwork.jpg");
        songData.IsAustralian.Should().BeTrue();
    }

    [Theory]
    [InlineData(Station.TripleJ, "triplej")]
    [InlineData(Station.DoubleJ, "doublej")]
    [InlineData(Station.Unearthed, "unearthed")]
    public void Station_Enum_HasCorrectValues(Station station, string expectedString)
    {
        // Act
        var stationString = station.ToString();

        // Assert
        stationString.Should().Be(expectedString);
    }

    [Theory]
    [InlineData(Region.NSW, "NSW")]
    [InlineData(Region.VIC, "VIC")]
    [InlineData(Region.QLD, "QLD")]
    [InlineData(Region.WA, "WA")]
    [InlineData(Region.SA, "SA")]
    [InlineData(Region.TAS, "TAS")]
    [InlineData(Region.ACT, "ACT")]
    [InlineData(Region.NT, "NT")]
    public void Region_Enum_HasCorrectValues(Region region, string expectedString)
    {
        // Act
        var regionString = region.ToString();

        // Assert
        regionString.Should().Be(expectedString);
    }

    [Fact]
    public void UserSettings_WithValidData_CreatesCorrectly()
    {
        // Arrange
        var lastStation = Station.DoubleJ;
        var lastVolume = 75;
        var lastUpdated = DateTime.UtcNow;

        // Act
        var userSettings = new UserSettings
        {
            LastStation = lastStation,
            LastVolume = lastVolume,
            LastUpdated = lastUpdated
        };

        // Assert
        userSettings.LastStation.Should().Be(lastStation);
        userSettings.LastVolume.Should().Be(lastVolume);
        userSettings.LastUpdated.Should().Be(lastUpdated);
    }

    [Fact]
    public void UserSettings_SerializesToJson_Correctly()
    {
        // Arrange
        var userSettings = new UserSettings
        {
            LastStation = Station.Unearthed,
            LastVolume = 60,
            LastUpdated = DateTime.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(userSettings, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        json.Should().Contain("unearthed");
        json.Should().Contain("60");
    }

    [Fact]
    public void UserSettings_DeserializesFromJson_Correctly()
    {
        // Arrange
        var json = @"{
            ""lastStation"": ""doubleJ"",
            ""lastVolume"": 80,
            ""lastUpdated"": ""2024-01-01T12:00:00Z""
        }";

        // Act
        var userSettings = JsonSerializer.Deserialize<UserSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        userSettings.Should().NotBeNull();
        userSettings!.LastStation.Should().Be(Station.DoubleJ);
        userSettings.LastVolume.Should().Be(80);
    }

    [Fact]
    public void NowPlayingResponse_WithValidData_CreatesCorrectly()
    {
        // Arrange
        var nowPlayingItem = new NowPlayingItem
        {
            recording = new Recording
            {
                Title = "Current Song",
                Artists = new List<Artist>
                {
                    new Artist { Name = "Current Artist" }
                }
            },
            PlayedTime = "2024-01-01T12:00:00Z"
        };

        // Act
        var response = new NowPlayingResponse
        {
            now = nowPlayingItem
        };

        // Assert
        response.now.Should().Be(nowPlayingItem);
        response.now!.recording.Title.Should().Be("Current Song");
        response.now.recording.Artists[0].Name.Should().Be("Current Artist");
    }
}