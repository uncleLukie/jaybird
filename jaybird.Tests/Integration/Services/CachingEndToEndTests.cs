using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using jaybird.Models;
using jaybird.Services;
using jaybird.Tests.TestInfrastructure.Helpers;
using static jaybird.Tests.TestInfrastructure.Helpers.DateTimeHelper;

namespace jaybird.Tests.Integration.Services
{
    public class CachingEndToEndTests : IDisposable
    {
        private readonly Mock<ILogger<SongCacheService>> _mockLogger;
        private readonly ITestOutputHelper _output;
        private readonly SongCacheService _cacheService;

        public CachingEndToEndTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger<SongCacheService>>();
            
            // Initialize DateTimeHelper with a fixed time
            DateTimeHelper.SetFixedDateTime(DateTime.UtcNow);
            
            _cacheService = new SongCacheService(_mockLogger.Object);
        }

        [Fact]
        public async Task CompleteCachingWorkflow_StoreRetrieveExpire_WorksCorrectly()
        {
            // Arrange
            var station = Station.TripleJ;
            var song1 = TestDataHelper.CreateTestSongData("Artist One", "Song One");
            var song2 = TestDataHelper.CreateTestSongData("Artist Two", "Song Two");
            var song3 = TestDataHelper.CreateTestSongData("Artist Three", "Song Three");

            // Act & Assert - Store songs
            await _cacheService.StoreSongAsync(station, song1);
            await _cacheService.StoreSongAsync(station, song2);
            await _cacheService.StoreSongAsync(station, song3);

            // Verify all songs are cached
            var cachedSongs = await _cacheService.GetCachedSongsAsync(station);
            cachedSongs.Should().HaveCount(3);
            cachedSongs.Should().Contain(s => s.Title == "Song One");
            cachedSongs.Should().Contain(s => s.Title == "Song Two");
            cachedSongs.Should().Contain(s => s.Title == "Song Three");

            // Verify latest song is returned
            var latestSong = await _cacheService.GetLatestSongAsync(station);
            latestSong.Should().NotBeNull();
            latestSong.Title.Should().Be("Song Three");

            // Act - Advance time to expire cache
            DateTimeHelper.SetFixedDateTime(DateTime.UtcNow.AddMinutes(15)); // Default TTL is 10 minutes

            // Assert - Cache should be expired
            var expiredSongs = await _cacheService.GetCachedSongsAsync(station);
            expiredSongs.Should().BeEmpty();

            var expiredLatest = await _cacheService.GetLatestSongAsync(station);
            expiredLatest.Should().BeNull();
        }

        [Fact]
        public async Task MultiStationCaching_DifferentStations_KeepsDataSeparate()
        {
            // Arrange
            var tripleJSong = TestDataHelper.CreateTestSongData("Triple J Artist", "Triple J Song");
            var doubleJSong = TestDataHelper.CreateTestSongData("Double J Artist", "Double J Song");
            var unearthedSong = TestDataHelper.CreateTestSongData("Unearthed Artist", "Unearthed Song");

            // Act
            await _cacheService.StoreSongAsync(Station.TripleJ, tripleJSong);
            await _cacheService.StoreSongAsync(Station.DoubleJ, doubleJSong);
            await _cacheService.StoreSongAsync(Station.Unearthed, unearthedSong);

            // Assert
            var tripleJSongs = await _cacheService.GetCachedSongsAsync(Station.TripleJ);
            var doubleJSongs = await _cacheService.GetCachedSongsAsync(Station.DoubleJ);
            var unearthedSongs = await _cacheService.GetCachedSongsAsync(Station.Unearthed);

            tripleJSongs.Should().HaveCount(1);
            tripleJSongs.First().Title.Should().Be("Triple J Song");

            doubleJSongs.Should().HaveCount(1);
            doubleJSongs.First().Title.Should().Be("Double J Song");

            unearthedSongs.Should().HaveCount(1);
            unearthedSongs.First().Title.Should().Be("Unearthed Song");
        }

        [Fact]
        public async Task CacheSizeLimit_ExceedsMaximum_RemovesOldestEntries()
        {
            // Arrange
            var station = Station.DoubleJ;
            var songs = new List<SongData>();

            // Create more songs than the cache limit (default is 100)
            for (int i = 0; i < 105; i++)
            {
                var song = TestDataHelper.CreateTestSongData($"Artist {i}", $"Song {i}");
                songs.Add(song);
                
                // Add small delay to ensure different timestamps
                DateTimeHelper.SetFixedDateTime(DateTime.UtcNow.AddSeconds(i + 1));
                await _cacheService.StoreSongAsync(station, song);
            }

            // Act
            var cachedSongs = await _cacheService.GetCachedSongsAsync(station);

            // Assert
            cachedSongs.Should().HaveCount(100); // Should be limited to max size
            
            // Should contain the most recent songs (songs 5-104)
            cachedSongs.Should().NotContain(s => s.Title == "Song 0");
            cachedSongs.Should().NotContain(s => s.Title == "Song 4");
            cachedSongs.Should().Contain(s => s.Title == "Song 104");
            
            // Latest song should be the last one added
            var latestSong = await _cacheService.GetLatestSongAsync(station);
            latestSong.Title.Should().Be("Song 104");
        }

        [Fact]
        public async Task ConcurrentCaching_MultipleThreads_HandlesRaceConditions()
        {
            // Arrange
            var station = Station.Unearthed;
            var tasks = new List<Task>();

            // Act - Concurrent store operations
            for (int i = 0; i < 50; i++)
            {
                var index = i;
                var task = Task.Run(async () =>
                {
                    var song = TestDataHelper.CreateTestSongData($"Concurrent Artist {index}", $"Concurrent Song {index}");
                    await _cacheService.StoreSongAsync(station, song);
                });
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            // Assert
            var cachedSongs = await _cacheService.GetCachedSongsAsync(station);
            cachedSongs.Should().HaveCount(50);
            
            // All songs should be present
            for (int i = 0; i < 50; i++)
            {
                cachedSongs.Should().Contain(s => s.Title == $"Concurrent Song {i}");
            }
        }

        [Fact]
        public async Task CacheExpiration_DifferentTtlSettings_UsesCorrectTiming()
        {
            // Arrange
            var station = Station.TripleJ;
            var song = TestDataHelper.CreateTestSongData("Test Artist", "Test Song");

            // Create cache with custom TTL
            var customCache = new SongCacheService(_mockLogger.Object, ttlMinutes: 5);

            // Act
            await customCache.StoreSongAsync(station, song);

            // Assert - Should be cached initially
            var immediateSongs = await customCache.GetCachedSongsAsync(station);
            immediateSongs.Should().HaveCount(1);

            // Advance time by 4 minutes (should still be cached)
            DateTimeHelper.SetFixedDateTime(DateTime.UtcNow.AddMinutes(4));
            var after4Minutes = await customCache.GetCachedSongsAsync(station);
            after4Minutes.Should().HaveCount(1);

            // Advance time by 2 more minutes (total 6 minutes, should be expired)
            DateTimeHelper.SetFixedDateTime(DateTime.UtcNow.AddMinutes(6));
            var after6Minutes = await customCache.GetCachedSongsAsync(station);
            after6Minutes.Should().BeEmpty();
        }

        [Fact]
        public async Task CacheWithNullValues_HandlesGracefully()
        {
            // Arrange
            var station = Station.DoubleJ;
            var songWithNulls = new SongData
            {
                Artist = "Test Artist",
                Title = "Test Title",
                Album = null, // Null album
                Duration = 180,
                StartTime = DateTime.UtcNow,
                IsLive = false,
                ArtworkUrl = null // Null artwork URL
            };

            // Act
            await _cacheService.StoreSongAsync(station, songWithNulls);

            // Assert
            var cachedSongs = await _cacheService.GetCachedSongsAsync(station);
            cachedSongs.Should().HaveCount(1);
            
            var cachedSong = cachedSongs.First();
            cachedSong.Artist.Should().Be("Test Artist");
            cachedSong.Title.Should().Be("Test Title");
            cachedSong.Album.Should().BeNull();
            cachedSong.ArtworkUrl.Should().BeNull();
        }

        [Fact]
        public async Task CacheCleanup_ExpiredEntries_RemovesAllExpiredData()
        {
            // Arrange
            var tripleJStation = Station.TripleJ;
            var doubleJStation = Station.DoubleJ;

            // Add songs to different stations at different times
            await _cacheService.StoreSongAsync(tripleJStation, TestDataHelper.CreateTestSongData("Artist 1", "Song 1"));
            
            DateTimeHelper.SetFixedDateTime(DateTime.UtcNow.AddMinutes(5));
            await _cacheService.StoreSongAsync(doubleJStation, TestDataHelper.CreateTestSongData("Artist 2", "Song 2"));
            
            DateTimeHelper.SetFixedDateTime(DateTime.UtcNow.AddMinutes(13)); // Total 13 minutes
            await _cacheService.StoreSongAsync(tripleJStation, TestDataHelper.CreateTestSongData("Artist 3", "Song 3"));

            // Act - Advance time to expire all entries
            DateTimeHelper.SetFixedDateTime(DateTime.UtcNow.AddMinutes(23)); // Total 23 minutes

            // Assert
            var tripleJSongs = await _cacheService.GetCachedSongsAsync(tripleJStation);
            var doubleJSongs = await _cacheService.GetCachedSongsAsync(doubleJStation);

            tripleJSongs.Should().BeEmpty();
            doubleJSongs.Should().BeEmpty();
        }

        [Fact]
        public async Task CachePerformance_LargeDataset_HandlesEfficiently()
        {
            // Arrange
            var station = Station.TripleJ;
            var startTime = DateTime.UtcNow;

            // Act - Add many songs
            for (int i = 0; i < 1000; i++)
            {
                var song = TestDataHelper.CreateTestSongData($"Artist {i}", $"Song {i}");
                await _cacheService.StoreSongAsync(station, song);
            }

            var storeTime = DateTime.UtcNow - startTime;

            // Measure retrieval performance
            var retrieveStart = DateTime.UtcNow;
            var cachedSongs = await _cacheService.GetCachedSongsAsync(station);
            var retrieveTime = DateTime.UtcNow - retrieveStart;

            // Assert
            cachedSongs.Should().HaveCount(100); // Limited to max size
            storeTime.Should().BeLessThan(TimeSpan.FromSeconds(5));
            retrieveTime.Should().BeLessThan(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task CacheWithDuplicateSongs_SameArtistTitle_StoresBothEntries()
        {
            // Arrange
            var station = Station.DoubleJ;
            var song1 = TestDataHelper.CreateTestSongData("Same Artist", "Same Title");
            var song2 = TestDataHelper.CreateTestSongData("Same Artist", "Same Title");

            // Act
            await _cacheService.StoreSongAsync(station, song1);
            _dateTimeHelper.AdvanceTime(TimeSpan.FromSeconds(1));
            await _cacheService.StoreSongAsync(station, song2);

            // Assert
            var cachedSongs = await _cacheService.GetCachedSongsAsync(station);
            cachedSongs.Should().HaveCount(2);
            
            // Both should have same artist and title but different timestamps
            cachedSongs.All(s => s.Artist == "Same Artist" && s.Title == "Same Title").Should().BeTrue();
            
            // Latest should be the second one (more recent timestamp)
            var latest = await _cacheService.GetLatestSongAsync(station);
            latest.StartTime.Should().Be(song2.StartTime);
        }

        public void Dispose()
        {
            _cacheService?.Dispose();
            DateTimeHelper.Reset();
        }
    }
}