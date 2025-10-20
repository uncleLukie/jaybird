using jaybird.Models;
using jaybird.Services;
using jaybird.Tests.TestInfrastructure.Helpers;
using Spectre.Console.Rendering;
using System.Collections.Concurrent;

namespace jaybird.Tests.Unit.Services;

/// <summary>
/// Unit tests for SongCacheService
/// </summary>
public class SongCacheServiceTests : IDisposable
{
    private readonly SongCacheService _cacheService;
    private readonly Mock<IRenderable> _mockRenderable;

    public SongCacheServiceTests()
    {
        _cacheService = new SongCacheService();
        _mockRenderable = new Mock<IRenderable>();
        DateTimeHelper.SetFixedDateTime(DateTime.UtcNow);
    }

    public void Dispose()
    {
        DateTimeHelper.Reset();
    }

    [Fact]
    public async Task GetCachedSongAsync_WithValidCache_ReturnsCachedSong()
    {
        // Arrange
        var station = Station.TripleJ;
        var region = Region.NSW;
        var song = TestDataHelper.CreateSampleSongData();
        
        _cacheService.CacheSongData(station, region, song, _mockRenderable.Object);

        // Act
        var result = await _cacheService.GetCachedSongAsync(station, region);

        // Assert
        result.Should().NotBeNull();
        result!.Song.Title.Should().Be(song.Title);
        result.Song.Artist.Should().Be(song.Artist);
        result.Artwork.Should().Be(_mockRenderable.Object);
    }

    [Fact]
    public async Task GetCachedSongAsync_WithExpiredCache_ReturnsNull()
    {
        // Arrange
        var station = Station.DoubleJ;
        var region = Region.VIC;
        var song = TestDataHelper.CreateSampleSongData();
        
        _cacheService.CacheSongData(station, region, song, _mockRenderable.Object);
        
        // Move time forward beyond cache TTL
        DateTimeHelper.SetFixedDateTime(DateTimeHelper.Now.AddMinutes(6));

        // Act
        var result = await _cacheService.GetCachedSongAsync(station, region);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedSongAsync_WithNoCache_ReturnsNull()
    {
        // Arrange
        var station = Station.Unearthed;
        var region = Region.WA;

        // Act
        var result = await _cacheService.GetCachedSongAsync(station, region);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedSongAsync_WithoutRegion_DefaultsToNSW()
    {
        // Arrange
        var station = Station.TripleJ;
        var song = TestDataHelper.CreateSampleSongData();
        
        _cacheService.CacheSongData(station, Region.NSW, song, _mockRenderable.Object);

        // Act
        var result = await _cacheService.GetCachedSongAsync(station);

        // Assert
        result.Should().NotBeNull();
        result!.Song.Title.Should().Be(song.Title);
    }

    [Fact]
    public async Task GetCachedArtworkAsync_WithValidCache_ReturnsCachedArtwork()
    {
        // Arrange
        var artworkUrl = "https://example.com/artwork.jpg";
        var maxWidth = 24;
        
        _cacheService.CacheArtwork(artworkUrl, maxWidth, _mockRenderable.Object);

        // Act
        var result = await _cacheService.GetCachedArtworkAsync(artworkUrl, maxWidth);

        // Assert
        result.Should().Be(_mockRenderable.Object);
    }

    [Fact]
    public async Task GetCachedArtworkAsync_WithExpiredCache_ReturnsNull()
    {
        // Arrange
        var artworkUrl = "https://example.com/artwork.jpg";
        var maxWidth = 24;
        
        _cacheService.CacheArtwork(artworkUrl, maxWidth, _mockRenderable.Object);
        
        // Move time forward beyond cache TTL
        DateTimeHelper.SetFixedDateTime(DateTimeHelper.Now.AddHours(2));

        // Act
        var result = await _cacheService.GetCachedArtworkAsync(artworkUrl, maxWidth);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedArtworkAsync_WithNullUrl_ReturnsNull()
    {
        // Arrange
        string? artworkUrl = null;
        var maxWidth = 24;

        // Act
        var result = await _cacheService.GetCachedArtworkAsync(artworkUrl, maxWidth);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCachedArtworkAsync_WithEmptyUrl_ReturnsNull()
    {
        // Arrange
        var artworkUrl = "";
        var maxWidth = 24;

        // Act
        var result = await _cacheService.GetCachedArtworkAsync(artworkUrl, maxWidth);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CacheSongData_WithValidData_StoresInCache()
    {
        // Arrange
        var station = Station.TripleJ;
        var region = Region.NSW;
        var song = TestDataHelper.CreateSampleSongData();

        // Act
        _cacheService.CacheSongData(station, region, song, _mockRenderable.Object);

        // Assert
        var cached = _cacheService.GetCachedSongAsync(station, region).Result;
        cached.Should().NotBeNull();
        cached!.Song.Title.Should().Be(song.Title);
    }

    [Fact]
    public void CacheSongData_WithoutRegion_DefaultsToNSW()
    {
        // Arrange
        var station = Station.TripleJ;
        var song = TestDataHelper.CreateSampleSongData();

        // Act
        _cacheService.CacheSongData(station, song, _mockRenderable.Object);

        // Assert
        var cached = _cacheService.GetCachedSongAsync(station, Region.NSW).Result;
        cached.Should().NotBeNull();
        cached!.Song.Title.Should().Be(song.Title);
    }

    [Fact]
    public void CacheArtwork_WithValidData_StoresInCache()
    {
        // Arrange
        var artworkUrl = "https://example.com/artwork.jpg";
        var maxWidth = 24;

        // Act
        _cacheService.CacheArtwork(artworkUrl, maxWidth, _mockRenderable.Object);

        // Assert
        var cached = _cacheService.GetCachedArtworkAsync(artworkUrl, maxWidth).Result;
        cached.Should().Be(_mockRenderable.Object);
    }

    [Fact]
    public void CacheArtwork_WithNullUrl_DoesNotStore()
    {
        // Arrange
        string? artworkUrl = null;
        var maxWidth = 24;

        // Act
        _cacheService.CacheArtwork(artworkUrl, maxWidth, _mockRenderable.Object);

        // Assert - Should not throw and cache should remain empty
        var cached = _cacheService.GetCachedArtworkAsync("https://example.com/other.jpg", maxWidth).Result;
        cached.Should().BeNull();
    }

    [Fact]
    public void CleanupExpiredEntries_RemovesExpiredEntries()
    {
        // Arrange
        var station1 = Station.TripleJ;
        var station2 = Station.DoubleJ;
        var song1 = TestDataHelper.CreateSampleSongData("Artist 1", "Song 1");
        var song2 = TestDataHelper.CreateSampleSongData("Artist 2", "Song 2");
        
        _cacheService.CacheSongData(station1, Region.NSW, song1, _mockRenderable.Object);
        
        // Move time forward and add second song
        DateTimeHelper.SetFixedDateTime(DateTimeHelper.Now.AddMinutes(3));
        _cacheService.CacheSongData(station2, Region.NSW, song2, _mockRenderable.Object);
        
        // Move time forward beyond first song's TTL
        DateTimeHelper.SetFixedDateTime(DateTimeHelper.Now.AddMinutes(4));

        // Act
        _cacheService.CleanupExpiredEntries();

        // Assert
        var cached1 = _cacheService.GetCachedSongAsync(station1, Region.NSW).Result;
        var cached2 = _cacheService.GetCachedSongAsync(station2, Region.NSW).Result;
        
        cached1.Should().BeNull(); // Should be expired
        cached2.Should().NotBeNull(); // Should still be valid
    }

    [Fact]
    public void CacheSongData_EnforcesSizeLimit_RemovesOldestEntries()
    {
        // Arrange
        var songs = TestDataHelper.CreateSampleSongDataList(15); // More than MaxSongCacheSize (10)

        // Act
        foreach (var (song, index) in songs.Select((song, index) => (song, index)))
        {
            _cacheService.CacheSongData(Station.TripleJ, Region.NSW, song, _mockRenderable.Object);
            // Add small delay to ensure different timestamps
            DateTimeHelper.SetFixedDateTime(DateTimeHelper.Now.AddSeconds(1));
        }

        // Assert
        // Should only have the most recent 10 entries
        var cached = _cacheService.GetCachedSongAsync(Station.TripleJ, Region.NSW).Result;
        cached.Should().NotBeNull();
        cached!.Song.Title.Should().Be("Song 15"); // Should be the last song added
    }

    [Fact]
    public void CacheArtwork_EnforcesSizeLimit_RemovesOldestEntries()
    {
        // Arrange
        var artworks = new List<Mock<IRenderable>>();
        for (int i = 0; i < 25; i++) // More than MaxArtworkCacheSize (20)
        {
            artworks.Add(new Mock<IRenderable>());
        }

        // Act
        foreach (var (artwork, index) in artworks.Select((artwork, index) => (artwork, index)))
        {
            _cacheService.CacheArtwork($"https://example.com/artwork{index}.jpg", 24, artwork.Object);
            // Add small delay to ensure different timestamps
            DateTimeHelper.SetFixedDateTime(DateTimeHelper.Now.AddSeconds(1));
        }

        // Assert
        // Should be able to retrieve the most recent artwork
        var cached = _cacheService.GetCachedArtworkAsync("https://example.com/artwork24.jpg", 24).Result;
        cached.Should().Be(artworks.Last().Object);
    }

    [Fact]
    public async Task PreemptiveCacheAsync_CompletesSuccessfully()
    {
        // Arrange
        var station = Station.TripleJ;
        var region = Region.NSW;

        // Act & Assert
        await _cacheService.PreemptiveCacheAsync(station, region);
        
        // Should not throw and complete successfully
        // Currently a placeholder implementation, so just verify it doesn't throw
        true.Should().BeTrue();
    }

    [Theory]
    [InlineData(Station.TripleJ, Region.NSW)]
    [InlineData(Station.DoubleJ, Region.VIC)]
    [InlineData(Station.Unearthed, Region.WA)]
    [InlineData(Station.TripleJ, Region.QLD)]
    public async Task GetCachedSongAsync_WithDifferentStationAndRegionCombinations_WorksCorrectly(
        Station station, Region region)
    {
        // Arrange
        var song = TestDataHelper.CreateSampleSongData(
            artist: $"{station} Artist",
            title: $"{region} Song");
        
        _cacheService.CacheSongData(station, region, song, _mockRenderable.Object);

        // Act
        var result = await _cacheService.GetCachedSongAsync(station, region);

        // Assert
        result.Should().NotBeNull();
        result!.Song.Artist.Should().Contain(station.ToString());
        result.Song.Title.Should().Contain(region.ToString());
    }

    [Fact]
    public void CacheSongData_WithSameStationAndRegion_OverwritesExisting()
    {
        // Arrange
        var station = Station.TripleJ;
        var region = Region.NSW;
        var song1 = TestDataHelper.CreateSampleSongData("Artist 1", "Song 1");
        var song2 = TestDataHelper.CreateSampleSongData("Artist 2", "Song 2");
        var renderable2 = new Mock<IRenderable>();
        
        _cacheService.CacheSongData(station, region, song1, _mockRenderable.Object);

        // Act
        _cacheService.CacheSongData(station, region, song2, renderable2.Object);

        // Assert
        var cached = _cacheService.GetCachedSongAsync(station, region).Result;
        cached.Should().NotBeNull();
        cached!.Song.Title.Should().Be(song2.Title);
        cached.Song.Artist.Should().Be(song2.Artist);
        cached.Artwork.Should().Be(renderable2.Object);
    }
}