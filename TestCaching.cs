using System;
using System.Threading.Tasks;
using jaybird.Models;
using jaybird.Services;
using jaybird.Utils;
using Microsoft.Extensions.Configuration;

namespace jaybird.Test
{
    public class TestCaching
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Testing jaybird caching system...");
            
            // Load configuration
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("config/appsettings.json", optional: false)
                .Build();
            
            var appConfig = config.Get<AppConfig>();
            
            // Create services
            var settingsService = new SettingsService();
            var songCacheService = new SongCacheService();
            var songRetrievalService = new SongRetrievalService(appConfig.Api);
            var artworkRenderer = new ArtworkRenderer();
            
            // Test 1: Cache miss scenario
            Console.WriteLine("\n=== Test 1: Cache Miss ===");
            var songData = await songRetrievalService.GetCurrentSongAsync(Station.TripleJ);
            if (songData != null)
            {
                Console.WriteLine($"✓ Retrieved song: {songData.Artist} - {songData.Title}");
                
                // Cache the result
                songCacheService.SetCachedSong(Station.TripleJ, songData);
                Console.WriteLine("✓ Cached song data");
            }
            else
            {
                Console.WriteLine("✗ Failed to retrieve song data");
            }
            
            // Test 2: Cache hit scenario
            Console.WriteLine("\n=== Test 2: Cache Hit ===");
            var cachedSong = songCacheService.GetCachedSong(Station.TripleJ);
            if (cachedSong != null)
            {
                Console.WriteLine($"✓ Retrieved cached song: {cachedSong.Artist} - {cachedSong.Title}");
            }
            else
            {
                Console.WriteLine("✗ Failed to retrieve cached song");
            }
            
            // Test 3: Artwork caching
            Console.WriteLine("\n=== Test 3: Artwork Caching ===");
            if (songData?.ArtworkUrl != null)
            {
                var cachedArtwork = songCacheService.GetCachedArtwork(songData.ArtworkUrl, 40);
                if (cachedArtwork != null)
                {
                    Console.WriteLine("✓ Found cached artwork");
                }
                else
                {
                    Console.WriteLine("✗ No cached artwork found");
                }
            }
            
            Console.WriteLine("\n=== Test Complete ===");
        }
    }
}