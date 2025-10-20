using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;
using Xunit.Abstractions;
using jaybird.Models;
using jaybird.Services;

namespace jaybird.Tests.Integration.Services
{
    public class SongRetrievalServiceIntegrationTests : IDisposable
    {
        private readonly WireMockServer _mockServer;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<SongRetrievalService>> _mockLogger;
        private readonly ITestOutputHelper _output;
        private readonly SongRetrievalService _service;

        public SongRetrievalServiceIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger<SongRetrievalService>>();
            
            // Start WireMock server
            _mockServer = WireMockServer.Start();
            _httpClient = new HttpClient { BaseAddress = new Uri(_mockServer.Url!) };

            // Create API config
            var apiConfig = new ApiConfig
            {
                BaseUrl = _mockServer.Url!,
                TripleJApi = "/triplej",
                DoubleJApi = "/doublej",
                UnearthedApi = "/unearthed"
            };

            _service = new SongRetrievalService(_httpClient, apiConfig, _mockLogger.Object);
        }

        [Fact]
        public async Task GetCurrentSongAsync_WithValidTripleJApi_ReturnsSongData()
        {
            // Arrange
            var expectedSong = new SongData
            {
                Artist = "Flume",
                Title = "Say It (feat. Tove Lo)",
                Album = "Skin",
                Duration = 223,
                StartTime = DateTime.UtcNow,
                IsLive = false,
                ArtworkUrl = "https://example.com/artwork.jpg"
            };

            var jsonResponse = @"{
                ""artist"": ""Flume"",
                ""title"": ""Say It (feat. Tove Lo)"",
                ""album"": ""Skin"",
                ""duration"": 223,
                ""startTime"": """ + DateTime.UtcNow.ToString("O") + @""",
                ""isLive"": false,
                ""artworkUrl"": ""https://example.com/artwork.jpg""
            }";

            _mockServer
                .Given(Request.Create().WithPath("/triplej").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(jsonResponse));

            // Act
            var result = await _service.GetCurrentSongAsync(Station.TripleJ);

            // Assert
            result.Should().NotBeNull();
            result.Artist.Should().Be(expectedSong.Artist);
            result.Title.Should().Be(expectedSong.Title);
            result.Album.Should().Be(expectedSong.Album);
            result.Duration.Should().Be(expectedSong.Duration);
            result.IsLive.Should().Be(expectedSong.IsLive);
            result.ArtworkUrl.Should().Be(expectedSong.ArtworkUrl);
        }

        [Fact]
        public async Task GetCurrentSongAsync_WithETag_ReturnsNotModifiedOnSecondCall()
        {
            // Arrange
            var jsonResponse = @"{
                ""artist"": ""Tame Impala"",
                ""title"": ""The Less I Know The Better"",
                ""album"": ""Currents"",
                ""duration"": 206,
                ""startTime"": """ + DateTime.UtcNow.ToString("O") + @""",
                ""isLive"": false,
                ""artworkUrl"": ""https://example.com/artwork2.jpg""
            }";

            var etag = "\"test-etag-123\"";

            _mockServer
                .Given(Request.Create().WithPath("/doublej").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithHeader("ETag", etag)
                    .WithBody(jsonResponse));

            // Act - First call
            var firstResult = await _service.GetCurrentSongAsync(Station.DoubleJ);
            firstResult.Should().NotBeNull();

            // Setup second response to return 304 Not Modified
            _mockServer
                .Given(Request.Create()
                    .WithPath("/doublej")
                    .WithHeader("If-None-Match", etag)
                    .UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.NotModified));

            // Act - Second call
            var secondResult = await _service.GetCurrentSongAsync(Station.DoubleJ);

            // Assert
            secondResult.Should().BeNull(); // Service returns null on 304
        }

        [Fact]
        public async Task GetCurrentSongAsync_WithApiError_ReturnsNull()
        {
            // Arrange
            _mockServer
                .Given(Request.Create().WithPath("/unearthed").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.InternalServerError));

            // Act
            var result = await _service.GetCurrentSongAsync(Station.Unearthed);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetCurrentSongAsync_WithInvalidJson_ReturnsNull()
        {
            // Arrange
            _mockServer
                .Given(Request.Create().WithPath("/triplej").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("{ invalid json }"));

            // Act
            var result = await _service.GetCurrentSongAsync(Station.TripleJ);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetCurrentSongAsync_WithNetworkTimeout_ReturnsNull()
        {
            // Arrange
            _mockServer
                .Given(Request.Create().WithPath("/doublej").UsingGet())
                .RespondWith(Response.Create()
                    .WithDelay(TimeSpan.FromSeconds(5)) // Longer than HttpClient timeout
                    .WithStatusCode(HttpStatusCode.OK));

            // Create HttpClient with short timeout
            var timeoutHttpClient = new HttpClient { BaseAddress = new Uri(_mockServer.Url!), Timeout = TimeSpan.FromSeconds(1) };
            var timeoutService = new SongRetrievalService(timeoutHttpClient, new ApiConfig
            {
                BaseUrl = _mockServer.Url!,
                TripleJApi = "/triplej",
                DoubleJApi = "/doublej",
                UnearthedApi = "/unearthed"
            }, _mockLogger.Object);

            // Act
            var result = await timeoutService.GetCurrentSongAsync(Station.DoubleJ);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetCurrentSongAsync_WithLiveBroadcasting_ReturnsLiveSongData()
        {
            // Arrange
            var jsonResponse = @"{
                ""artist"": ""ABC News"",
                ""title"": ""Live: Morning News"",
                ""album"": """",
                ""duration"": 0,
                ""startTime"": """ + DateTime.UtcNow.ToString("O") + @""",
                ""isLive"": true,
                ""artworkUrl"": """"
            }";

            _mockServer
                .Given(Request.Create().WithPath("/triplej").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(jsonResponse));

            // Act
            var result = await _service.GetCurrentSongAsync(Station.TripleJ);

            // Assert
            result.Should().NotBeNull();
            result.IsLive.Should().BeTrue();
            result.Artist.Should().Be("ABC News");
            result.Title.Should().Contain("Live");
            result.Duration.Should().Be(0);
        }

        [Fact]
        public async Task GetCurrentSongAsync_ConcurrentCalls_HandlesMultipleStations()
        {
            // Arrange
            var tripleJResponse = @"{ ""artist"": ""Artist1"", ""title"": ""Song1"" }";
            var doubleJResponse = @"{ ""artist"": ""Artist2"", ""title"": ""Song2"" }";
            var unearthedResponse = @"{ ""artist"": ""Artist3"", ""title"": ""Song3"" }";

            _mockServer
                .Given(Request.Create().WithPath("/triplej").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(tripleJResponse));

            _mockServer
                .Given(Request.Create().WithPath("/doublej").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(doubleJResponse));

            _mockServer
                .Given(Request.Create().WithPath("/unearthed").UsingGet())
                .RespondWith(Response.Create()
                    .WithStatusCode(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(unearthedResponse));

            // Act
            var tasks = new[]
            {
                _service.GetCurrentSongAsync(Station.TripleJ),
                _service.GetCurrentSongAsync(Station.DoubleJ),
                _service.GetCurrentSongAsync(Station.Unearthed)
            };

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(3);
            results[0].Should().NotBeNull();
            results[0].Artist.Should().Be("Artist1");
            results[1].Should().NotBeNull();
            results[1].Artist.Should().Be("Artist2");
            results[2].Should().NotBeNull();
            results[2].Artist.Should().Be("Artist3");
        }

        public void Dispose()
        {
            _mockServer?.Stop();
            _mockServer?.Dispose();
            _httpClient?.Dispose();
        }
    }
}