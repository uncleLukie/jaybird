using jaybird.Models;
using Spectre.Console.Rendering;
using System.Collections.Concurrent;

namespace jaybird.Services;

public interface ISongCacheService
{
    Task<CachedSongData?> GetCachedSongAsync(Station station);
    Task<IRenderable?> GetCachedArtworkAsync(string? artworkUrl, int maxWidth);
    void CacheSongData(Station station, SongData song, IRenderable? artwork);
    void CacheArtwork(string? artworkUrl, int maxWidth, IRenderable renderable);
    void CleanupExpiredEntries();
    Task PreemptiveCacheAsync(Station station);
}

public class SongCacheService : ISongCacheService
{
    private readonly ConcurrentDictionary<string, CachedSongData> _songCache = new();
    private readonly ConcurrentDictionary<string, CachedArtwork> _artworkCache = new();
    private readonly object _cleanupLock = new object();
    private DateTime _lastCleanup = DateTime.MinValue;
    
    // Cache TTL settings
    private static readonly TimeSpan SongCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ArtworkCacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);
    
    // Cache limits to prevent memory bloat
    private const int MaxSongCacheSize = 10;
    private const int MaxArtworkCacheSize = 20;

    public async Task<CachedSongData?> GetCachedSongAsync(Station station)
    {
        var cacheKey = GenerateSongCacheKey(station);
        
        if (_songCache.TryGetValue(cacheKey, out var cachedSong))
        {
            // Check if cache entry is still valid
            if (DateTime.Now - cachedSong.CachedAt < SongCacheTtl)
            {
                Utils.DebugLogger.Log($"Cache HIT for station {station}: {cachedSong.Song.Title} by {cachedSong.Song.Artist}", "SongCacheService");
                return cachedSong;
            }
            else
            {
                // Remove expired entry
                _songCache.TryRemove(cacheKey, out _);
                Utils.DebugLogger.Log($"Cache EXPIRED for station {station}", "SongCacheService");
            }
        }
        
        Utils.DebugLogger.Log($"Cache MISS for station {station}", "SongCacheService");
        return null;
    }

    public async Task<IRenderable?> GetCachedArtworkAsync(string? artworkUrl, int maxWidth)
    {
        if (string.IsNullOrEmpty(artworkUrl))
        {
            return null;
        }

        var cacheKey = GenerateArtworkCacheKey(artworkUrl, maxWidth);
        
        if (_artworkCache.TryGetValue(cacheKey, out var cachedArtwork))
        {
            // Check if cache entry is still valid
            if (DateTime.Now - cachedArtwork.CachedAt < ArtworkCacheTtl)
            {
                Utils.DebugLogger.Log($"Artwork cache HIT for {artworkUrl}", "SongCacheService");
                return cachedArtwork.Renderable;
            }
            else
            {
                // Remove expired entry
                _artworkCache.TryRemove(cacheKey, out _);
                Utils.DebugLogger.Log($"Artwork cache EXPIRED for {artworkUrl}", "SongCacheService");
            }
        }
        
        Utils.DebugLogger.Log($"Artwork cache MISS for {artworkUrl}", "SongCacheService");
        return null;
    }

    public void CacheSongData(Station station, SongData song, IRenderable? artwork)
    {
        var cacheKey = GenerateSongCacheKey(station);
        var cachedSong = new CachedSongData
        {
            Song = song,
            Artwork = artwork,
            CachedAt = DateTime.Now,
            CacheKey = cacheKey
        };

        _songCache.AddOrUpdate(cacheKey, cachedSong, (key, oldValue) => cachedSong);
        
        // Enforce cache size limit
        EnforceSongCacheLimit();
        
        Utils.DebugLogger.Log($"Cached song for station {station}: {song.Title} by {song.Artist}", "SongCacheService");
    }

    public void CacheArtwork(string? artworkUrl, int maxWidth, IRenderable renderable)
    {
        if (string.IsNullOrEmpty(artworkUrl))
        {
            return;
        }

        var cacheKey = GenerateArtworkCacheKey(artworkUrl, maxWidth);
        var cachedArtwork = new CachedArtwork
        {
            Renderable = renderable,
            CachedAt = DateTime.Now,
            CacheKey = cacheKey
        };

        _artworkCache.AddOrUpdate(cacheKey, cachedArtwork, (key, oldValue) => cachedArtwork);
        
        // Enforce cache size limit
        EnforceArtworkCacheLimit();
        
        Utils.DebugLogger.Log($"Cached artwork for {artworkUrl} (width: {maxWidth})", "SongCacheService");
    }

    public void CleanupExpiredEntries()
    {
        lock (_cleanupLock)
        {
            var now = DateTime.Now;
            
            // Only run cleanup at intervals
            if (now - _lastCleanup < CleanupInterval)
            {
                return;
            }
            
            _lastCleanup = now;
            
            // Cleanup expired song cache entries
            var expiredSongKeys = new List<string>();
            foreach (var kvp in _songCache)
            {
                if (now - kvp.Value.CachedAt >= SongCacheTtl)
                {
                    expiredSongKeys.Add(kvp.Key);
                }
            }
            
            foreach (var key in expiredSongKeys)
            {
                _songCache.TryRemove(key, out _);
            }
            
            // Cleanup expired artwork cache entries
            var expiredArtworkKeys = new List<string>();
            foreach (var kvp in _artworkCache)
            {
                if (now - kvp.Value.CachedAt >= ArtworkCacheTtl)
                {
                    expiredArtworkKeys.Add(kvp.Key);
                }
            }
            
            foreach (var key in expiredArtworkKeys)
            {
                _artworkCache.TryRemove(key, out _);
            }
            
            if (expiredSongKeys.Count > 0 || expiredArtworkKeys.Count > 0)
            {
                Utils.DebugLogger.Log($"Cleanup completed: {expiredSongKeys.Count} songs, {expiredArtworkKeys.Count} artworks removed", "SongCacheService");
            }
        }
    }

    public async Task PreemptiveCacheAsync(Station station)
    {
        // This method can be called to pre-populate cache for a station
        // Implementation would depend on having access to song retrieval service
        // For now, this is a placeholder for future enhancement
        Utils.DebugLogger.Log($"Preemptive cache requested for station {station}", "SongCacheService");
        await Task.CompletedTask;
    }

    private static string GenerateSongCacheKey(Station station)
    {
        return $"song_{station}";
    }

    private static string GenerateArtworkCacheKey(string artworkUrl, int maxWidth)
    {
        // Create a hash of the URL to avoid issues with special characters
        var urlHash = artworkUrl.GetHashCode().ToString("X");
        return $"art_{urlHash}_{maxWidth}";
    }

    private void EnforceSongCacheLimit()
    {
        if (_songCache.Count <= MaxSongCacheSize)
        {
            return;
        }

        // Remove oldest entries (LRU-style)
        var entriesToRemove = _songCache
            .OrderBy(kvp => kvp.Value.CachedAt)
            .Take(_songCache.Count - MaxSongCacheSize)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in entriesToRemove)
        {
            _songCache.TryRemove(key, out _);
        }
        
        Utils.DebugLogger.Log($"Enforced song cache limit: removed {entriesToRemove.Count} entries", "SongCacheService");
    }

    private void EnforceArtworkCacheLimit()
    {
        if (_artworkCache.Count <= MaxArtworkCacheSize)
        {
            return;
        }

        // Remove oldest entries (LRU-style)
        var entriesToRemove = _artworkCache
            .OrderBy(kvp => kvp.Value.CachedAt)
            .Take(_artworkCache.Count - MaxArtworkCacheSize)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in entriesToRemove)
        {
            _artworkCache.TryRemove(key, out _);
        }
        
        Utils.DebugLogger.Log($"Enforced artwork cache limit: removed {entriesToRemove.Count} entries", "SongCacheService");
    }
}

public class CachedSongData
{
    public SongData Song { get; set; } = null!;
    public IRenderable? Artwork { get; set; }
    public DateTime CachedAt { get; set; }
    public string CacheKey { get; set; } = string.Empty;
}

public class CachedArtwork
{
    public IRenderable Renderable { get; set; } = null!;
    public DateTime CachedAt { get; set; }
    public string CacheKey { get; set; } = string.Empty;
}