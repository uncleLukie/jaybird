using jaybird.Services;
using jaybird.Tests.TestInfrastructure.Helpers;
using jaybird.Utils;
using Spectre.Console.Rendering;
using System.Net;

namespace jaybird.Tests.Unit.Utils;

/// <summary>
/// Unit tests for ArtworkRenderer
/// </summary>
public class ArtworkRendererTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ISongCacheService> _mockCacheService;
    private readonly string _testImagePath;

    public ArtworkRendererTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _mockCacheService = new Mock<ISongCacheService>();
        _testImagePath = Path.Combine(Path.GetTempPath(), "test_image.png");

        // Create a test image file
        CreateTestImageFile();
    }

    private void CreateTestImageFile()
    {
        // Create a simple 1x1 PNG file (minimal valid PNG)
        var pngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk start
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1 image
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, // Bit depth, color type
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, // IDAT chunk start
            0x54, 0x08, 0x99, 0x01, 0x01, 0x01, 0x00, 0x00, // Compressed image data
            0xFE, 0xFF, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, // More data
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, // IEND chunk
            0xAE, 0x42, 0x60, 0x82 // PNG end
        };
        File.WriteAllBytes(_testImagePath, pngBytes);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        if (File.Exists(_testImagePath))
        {
            File.Delete(_testImagePath);
        }
    }

    [Fact]
    public async Task RenderArtworkAsync_WithNullUrl_ReturnsNull()
    {
        // Arrange
        string? artworkUrl = null;
        var maxWidth = 24;

        // Act
        var result = await ArtworkRenderer.RenderArtworkAsync(artworkUrl, maxWidth, _mockCacheService.Object);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RenderArtworkAsync_WithEmptyUrl_ReturnsNull()
    {
        // Arrange
        var artworkUrl = "";
        var maxWidth = 24;

        // Act
        var result = await ArtworkRenderer.RenderArtworkAsync(artworkUrl, maxWidth, _mockCacheService.Object);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RenderArtworkAsync_WithWhitespaceUrl_ReturnsNull()
    {
        // Arrange
        var artworkUrl = "   ";
        var maxWidth = 24;

        // Act
        var result = await ArtworkRenderer.RenderArtworkAsync(artworkUrl, maxWidth, _mockCacheService.Object);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RenderArtworkAsync_WithCachedArtwork_ReturnsCachedResult()
    {
        // Arrange
        var artworkUrl = "https://example.com/artwork.jpg";
        var maxWidth = 24;
        var cachedRenderable = TestDataHelper.CreateMockRenderable();

        _mockCacheService.Setup(x => x.GetCachedArtworkAsync(artworkUrl, maxWidth))
            .ReturnsAsync(cachedRenderable);

        // Act
        var result = await ArtworkRenderer.RenderArtworkAsync(artworkUrl, maxWidth, _mockCacheService.Object);

        // Assert
        result.Should().Be(cachedRenderable);
        _mockCacheService.Verify(x => x.GetCachedArtworkAsync(artworkUrl, maxWidth), Times.Once);
        _mockCacheService.Verify(x => x.CacheArtwork(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IRenderable>()), Times.Never);
    }

    [Fact]
    public async Task RenderArtworkAsync_WithValidUrl_DownloadsAndRenders()
    {
        // Arrange
        var artworkUrl = "https://example.com/artwork.jpg";
        var maxWidth = 24;
        var imageBytes = File.ReadAllBytes(_testImagePath);

        _mockCacheService.Setup(x => x.GetCachedArtworkAsync(artworkUrl, maxWidth))
            .ReturnsAsync((IRenderable?)null);

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(imageBytes)
            });

        // Act
        var result = await ArtworkRenderer.RenderArtworkAsync(artworkUrl, maxWidth, _mockCacheService.Object);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IRenderable>();
        _mockCacheService.Verify(x => x.GetCachedArtworkAsync(artworkUrl, maxWidth), Times.Once);
        _mockCacheService.Verify(x => x.CacheArtwork(artworkUrl, maxWidth, It.IsAny<IRenderable>()), Times.Once);
    }

    [Fact]
    public async Task RenderArtworkAsync_WithHttpFailure_ReturnsNull()
    {
        // Arrange
        var artworkUrl = "https://example.com/artwork.jpg";
        var maxWidth = 24;

        _mockCacheService.Setup(x => x.GetCachedArtworkAsync(artworkUrl, maxWidth))
            .ReturnsAsync((IRenderable?)null);

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        // Act
        var result = await ArtworkRenderer.RenderArtworkAsync(artworkUrl, maxWidth, _mockCacheService.Object);

        // Assert
        result.Should().BeNull();
        _mockCacheService.Verify(x => x.GetCachedArtworkAsync(artworkUrl, maxWidth), Times.Once);
        _mockCacheService.Verify(x => x.CacheArtwork(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IRenderable>()), Times.Never);
    }

    [Fact]
    public async Task RenderArtworkAsync_WithNetworkException_ReturnsNull()
    {
        // Arrange
        var artworkUrl = "https://example.com/artwork.jpg";
        var maxWidth = 24;

        _mockCacheService.Setup(x => x.GetCachedArtworkAsync(artworkUrl, maxWidth))
            .ReturnsAsync((IRenderable?)null);

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await ArtworkRenderer.RenderArtworkAsync(artworkUrl, maxWidth, _mockCacheService.Object);

        // Assert
        result.Should().BeNull();
        _mockCacheService.Verify(x => x.GetCachedArtworkAsync(artworkUrl, maxWidth), Times.Once);
        _mockCacheService.Verify(x => x.CacheArtwork(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IRenderable>()), Times.Never);
    }

    [Fact]
    public async Task RenderArtworkAsync_WithInvalidImageData_ReturnsNull()
    {
        // Arrange
        var artworkUrl = "https://example.com/invalid.jpg";
        var maxWidth = 24;
        var invalidBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 }; // Invalid image data

        _mockCacheService.Setup(x => x.GetCachedArtworkAsync(artworkUrl, maxWidth))
            .ReturnsAsync((IRenderable?)null);

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(invalidBytes)
            });

        // Act
        var result = await ArtworkRenderer.RenderArtworkAsync(artworkUrl, maxWidth, _mockCacheService.Object);

        // Assert
        result.Should().BeNull();
        _mockCacheService.Verify(x => x.GetCachedArtworkAsync(artworkUrl, maxWidth), Times.Once);
        _mockCacheService.Verify(x => x.CacheArtwork(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IRenderable>()), Times.Never);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(48)]
    public async Task RenderArtworkAsync_WithDifferentMaxWidths_RendersCorrectly(int maxWidth)
    {
        // Arrange
        var artworkUrl = "https://example.com/artwork.jpg";
        var imageBytes = File.ReadAllBytes(_testImagePath);

        _mockCacheService.Setup(x => x.GetCachedArtworkAsync(artworkUrl, maxWidth))
            .ReturnsAsync((IRenderable?)null);

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(imageBytes)
            });

        // Act
        var result = await ArtworkRenderer.RenderArtworkAsync(artworkUrl, maxWidth, _mockCacheService.Object);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IRenderable>();
        _mockCacheService.Verify(x => x.CacheArtwork(artworkUrl, maxWidth, It.IsAny<IRenderable>()), Times.Once);
    }

    [Fact]
    public async Task RenderArtworkAsync_WithoutCacheService_DownloadsAndRenders()
    {
        // Arrange
        var artworkUrl = "https://example.com/artwork.jpg";
        var maxWidth = 24;
        var imageBytes = File.ReadAllBytes(_testImagePath);

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(imageBytes)
            });

        // Act
        var result = await ArtworkRenderer.RenderArtworkAsync(artworkUrl, maxWidth);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IRenderable>();
    }

    [Fact]
    public async Task RenderArtworkAsync_WithCacheService_CachesResult()
    {
        // Arrange
        var artworkUrl = "https://example.com/artwork.jpg";
        var maxWidth = 24;
        var imageBytes = File.ReadAllBytes(_testImagePath);

        _mockCacheService.Setup(x => x.GetCachedArtworkAsync(artworkUrl, maxWidth))
            .ReturnsAsync((IRenderable?)null);

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(imageBytes)
            });

        // Act
        var result = await ArtworkRenderer.RenderArtworkAsync(artworkUrl, maxWidth, _mockCacheService.Object);

        // Assert
        result.Should().NotBeNull();
        _mockCacheService.Verify(x => x.CacheArtwork(artworkUrl, maxWidth, It.IsAny<IRenderable>()), Times.Once);
    }

    [Fact]
    public async Task RenderHeaderArtworkAsync_WithSmallTerminal_ReturnsNull()
    {
        // Arrange
        var artworkUrl = "https://example.com/artwork.jpg";
        var terminalHeight = 10; // Less than 14

        // Act
        var result = await ArtworkRenderer.RenderHeaderArtworkAsync(artworkUrl, terminalHeight);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RenderHeaderArtworkAsync_WithLargeTerminal_RendersArtwork()
    {
        // Arrange
        var artworkUrl = "https://example.com/artwork.jpg";
        var terminalHeight = 20; // Greater than 14
        var imageBytes = File.ReadAllBytes(_testImagePath);

        _mockCacheService.Setup(x => x.GetCachedArtworkAsync(artworkUrl, 8))
            .ReturnsAsync((IRenderable?)null);

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(imageBytes)
            });

        // Act
        var result = await ArtworkRenderer.RenderHeaderArtworkAsync(artworkUrl, terminalHeight, _mockCacheService.Object);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IRenderable>();
        _mockCacheService.Verify(x => x.CacheArtwork(artworkUrl, 8, It.IsAny<IRenderable>()), Times.Once);
    }

    [Theory]
    [InlineData(10, 50, 0)]  // Too small
    [InlineData(25, 80, 15)] // Small terminal
    [InlineData(35, 120, 25)] // Medium terminal
    [InlineData(50, 150, 35)] // Large terminal
    public void CalculateArtworkWidth_WithDifferentTerminalSizes_ReturnsCorrectWidth(
        int terminalHeight, int terminalWidth, int expectedWidth)
    {
        // Act
        var result = ArtworkRenderer.CalculateArtworkWidth(terminalHeight, terminalWidth);

        // Assert
        result.Should().Be(expectedWidth);
    }

    [Theory]
    [InlineData(19, 60, 0)]  // Just below minimum
    [InlineData(20, 59, 0)]  // Width too small
    [InlineData(20, 60, 15)] // At minimum
    public void CalculateArtworkWidth_AtBoundaries_ReturnsCorrectValues(
        int terminalHeight, int terminalWidth, int expectedWidth)
    {
        // Act
        var result = ArtworkRenderer.CalculateArtworkWidth(terminalHeight, terminalWidth);

        // Assert
        result.Should().Be(expectedWidth);
    }

    [Fact]
    public async Task RenderArtworkAsync_WithMultipleCalls_UsesCacheEfficiently()
    {
        // Arrange
        var artworkUrl = "https://example.com/artwork.jpg";
        var maxWidth = 24;
        var cachedRenderable = TestDataHelper.CreateMockRenderable();

        _mockCacheService.Setup(x => x.GetCachedArtworkAsync(artworkUrl, maxWidth))
            .ReturnsAsync(cachedRenderable);

        // Act
        var result1 = await ArtworkRenderer.RenderArtworkAsync(artworkUrl, maxWidth, _mockCacheService.Object);
        var result2 = await ArtworkRenderer.RenderArtworkAsync(artworkUrl, maxWidth, _mockCacheService.Object);

        // Assert
        result1.Should().Be(cachedRenderable);
        result2.Should().Be(cachedRenderable);
        _mockCacheService.Verify(x => x.GetCachedArtworkAsync(artworkUrl, maxWidth), Times.Exactly(2));
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}