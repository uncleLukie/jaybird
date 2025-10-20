namespace jaybird.Utils;

using Spectre.Console;
using Spectre.Console.Rendering;
using jaybird.Services;

public static class ArtworkRenderer
{
    private static readonly HttpClient _httpClient = new();

    /// <summary>
    /// Downloads an image from a URL and renders it as ASCII art.
    /// Returns null if the artwork URL is null, download fails, or rendering fails.
    /// </summary>
    /// <param name="artworkUrl">The URL of the artwork image</param>
    /// <param name="maxWidth">Maximum width in characters (adaptive based on terminal size)</param>
    /// <param name="songCacheService">Optional cache service for artwork caching</param>
    /// <returns>A CanvasImage renderable, or null if unavailable</returns>
    public static async Task<IRenderable?> RenderArtworkAsync(string? artworkUrl, int maxWidth, ISongCacheService? songCacheService = null)
    {
        if (string.IsNullOrEmpty(artworkUrl))
        {
            DebugLogger.Log("No artwork URL provided", "ArtworkRenderer");
            return null;
        }

        // Check cache first if available
        if (songCacheService != null)
        {
            var cachedArtwork = await songCacheService.GetCachedArtworkAsync(artworkUrl, maxWidth);
            if (cachedArtwork != null)
            {
                return cachedArtwork;
            }
        }

        try
        {
            DebugLogger.Log($"Downloading artwork from: {artworkUrl}", "ArtworkRenderer");

            // Download the image
            var imageBytes = await _httpClient.GetByteArrayAsync(artworkUrl);

            // Create a temporary file (CanvasImage requires a file path or stream)
            var tempPath = Path.GetTempFileName();
            await File.WriteAllBytesAsync(tempPath, imageBytes);

            try
            {
                // Create the canvas image with adaptive sizing
                var canvasImage = new CanvasImage(tempPath);
                canvasImage.MaxWidth(maxWidth);
                
                // Use high-quality bicubic resampler for better image quality
                canvasImage.BicubicResampler();

                DebugLogger.Log($"Artwork rendered successfully (max width: {maxWidth}, quality: Bicubic)", "ArtworkRenderer");
                
                // Cache the rendered artwork if cache service is available
                if (songCacheService != null)
                {
                    songCacheService.CacheArtwork(artworkUrl, maxWidth, canvasImage);
                }
                
                return canvasImage;
            }
            finally
            {
                // Clean up the temp file
                try { File.Delete(tempPath); } catch { /* ignore cleanup errors */ }
            }
        }
        catch (HttpRequestException ex)
        {
            DebugLogger.LogException(ex, "ArtworkRenderer.RenderArtworkAsync (HTTP)");
            return null;
        }
        catch (Exception ex)
        {
            DebugLogger.LogException(ex, "ArtworkRenderer.RenderArtworkAsync");
            return null;
        }
    }

    /// <summary>
    /// Renders artwork specifically for the header display (fixed small size).
    /// </summary>
    /// <param name="artworkUrl">The URL of the artwork image</param>
    /// <param name="terminalHeight">Current terminal height (to determine if header is visible)</param>
    /// <returns>A CanvasImage renderable, or null if unavailable or terminal too small</returns>
    public static async Task<IRenderable?> RenderHeaderArtworkAsync(string? artworkUrl, int terminalHeight)
    {
        // Only show artwork in header when terminal is tall enough for standard/full layout (>= 14 lines)
        if (terminalHeight < 14)
        {
            return null;
        }

        // Fixed small size for header: 8 characters wide to better align with jaybird/station art
        return await RenderArtworkAsync(artworkUrl, 8);
    }

    /// <summary>
    /// Calculates the appropriate artwork width based on terminal dimensions.
    /// </summary>
    /// <param name="terminalHeight">Current terminal height in lines</param>
    /// <param name="terminalWidth">Current terminal width in columns</param>
    /// <returns>Recommended max width for artwork, or 0 if terminal too small</returns>
    public static int CalculateArtworkWidth(int terminalHeight, int terminalWidth)
    {
        // Don't show artwork in very small terminals
        if (terminalHeight < 20 || terminalWidth < 60)
        {
            return 0;
        }

        // Small terminals: 15 characters wide
        if (terminalHeight < 30 || terminalWidth < 80)
        {
            return Math.Min(15, terminalWidth / 3);
        }

        // Medium terminals: 25 characters wide
        if (terminalHeight < 40 || terminalWidth < 120)
        {
            return Math.Min(25, terminalWidth / 3);
        }

        // Large terminals: 35 characters wide
        return Math.Min(35, terminalWidth / 3);
    }
}
