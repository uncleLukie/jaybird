using jaybird.Models;
using jaybird.Utils;

namespace jaybird.Tests.Unit.Utils;

/// <summary>
/// Unit tests for StationAsciiArt
/// </summary>
public class StationAsciiArtTests
{
    [Fact]
    public void IncrementFrame_AdvancesFrame_Correctly()
    {
        // Arrange
        var initialFrame = StationAsciiArt.GetPlayingIndicator(true);

        // Act
        StationAsciiArt.IncrementFrame();
        var nextFrame = StationAsciiArt.GetPlayingIndicator(true);

        // Assert
        nextFrame.Should().NotBe(initialFrame);
    }

    [Fact]
    public void IncrementFrame_WrapsAfterFourFrames()
    {
        // Arrange
        var initialFrame = StationAsciiArt.GetPlayingIndicator(true);

        // Act
        for (int i = 0; i < 4; i++)
        {
            StationAsciiArt.IncrementFrame();
        }
        var finalFrame = StationAsciiArt.GetPlayingIndicator(true);

        // Assert
        finalFrame.Should().Be(initialFrame);
    }

    [Theory]
    [InlineData(0, "▶")]
    [InlineData(1, "▷")]
    [InlineData(2, "▶")]
    [InlineData(3, "▷")]
    public void GetPlayingIndicator_WithPlaying_ReturnsCorrectSymbols(int frameCount, string expectedSymbol)
    {
        // Arrange
        for (int i = 0; i < frameCount; i++)
        {
            StationAsciiArt.IncrementFrame();
        }

        // Act
        var result = StationAsciiArt.GetPlayingIndicator(true);

        // Assert
        result.Should().Be(expectedSymbol);
    }

    [Fact]
    public void GetPlayingIndicator_WithNotPlaying_ReturnsPauseSymbol()
    {
        // Act
        var result = StationAsciiArt.GetPlayingIndicator(false);

        // Assert
        result.Should().Be("⏸");
    }

    [Fact]
    public void GetStationArt_WithTripleJ_ReturnsTripleJArt()
    {
        // Arrange
        var station = Station.TripleJ;
        var isPlaying = true;

        // Act
        var result = StationAsciiArt.GetStationArt(station, isPlaying);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("╔");
        result.Should().Contain("╗");
        result.Should().Contain("╠");
        result.Should().Contain("╣");
        result.Should().Contain("╚");
        result.Should().Contain("╝");
    }

    [Fact]
    public void GetStationArt_WithDoubleJ_ReturnsDoubleJArt()
    {
        // Arrange
        var station = Station.DoubleJ;
        var isPlaying = true;

        // Act
        var result = StationAsciiArt.GetStationArt(station, isPlaying);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("╔");
        result.Should().Contain("╗");
        result.Should().Contain("╠");
        result.Should().Contain("╣");
        result.Should().Contain("╚");
        result.Should().Contain("╝");
        result.Should().Contain("╬"); // DoubleJ uses different pattern
    }

    [Fact]
    public void GetStationArt_WithUnearthed_ReturnsUnearthedArt()
    {
        // Arrange
        var station = Station.Unearthed;
        var isPlaying = true;

        // Act
        var result = StationAsciiArt.GetStationArt(station, isPlaying);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("╔");
        result.Should().Contain("╗");
        result.Should().Contain("╠");
        result.Should().Contain("╣");
        result.Should().Contain("╚");
        result.Should().Contain("╝");
        result.Should().Contain("▓"); // Unearthed uses different pattern
    }

    [Fact]
    public void GetStationArt_WithNotPlaying_ReturnsStaticArt()
    {
        // Arrange
        var station = Station.TripleJ;
        var isPlaying = false;

        // Act
        var result1 = StationAsciiArt.GetStationArt(station, isPlaying);
        StationAsciiArt.IncrementFrame();
        var result2 = StationAsciiArt.GetStationArt(station, isPlaying);

        // Assert
        result1.Should().Be(result2); // Should be the same when not playing
    }

    [Fact]
    public void GetStationArt_WithPlaying_ReturnsAnimatedArt()
    {
        // Arrange
        var station = Station.TripleJ;
        var isPlaying = true;

        // Act
        var result1 = StationAsciiArt.GetStationArt(station, isPlaying);
        StationAsciiArt.IncrementFrame();
        var result2 = StationAsciiArt.GetStationArt(station, isPlaying);

        // Assert
        result1.Should().NotBe(result2); // Should be different when playing
    }

    [Fact]
    public void GetJaybirdArt_ReturnsValidAsciiArt()
    {
        // Act
        var result = StationAsciiArt.GetJaybirdArt();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("░");
        result.Should().Contain("▒");
        result.Should().Contain("▓");
        result.Should().Contain("█");
    }

    [Theory]
    [InlineData(Station.TripleJ, true)]
    [InlineData(Station.TripleJ, false)]
    [InlineData(Station.DoubleJ, true)]
    [InlineData(Station.DoubleJ, false)]
    [InlineData(Station.Unearthed, true)]
    [InlineData(Station.Unearthed, false)]
    public void GetStationArt_WithAllStationsAndStates_ReturnsValidArt(Station station, bool isPlaying)
    {
        // Act
        var result = StationAsciiArt.GetStationArt(station, isPlaying);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("╔");
        result.Should().Contain("╗");
        result.Should().Contain("╚");
        result.Should().Contain("╝");
    }

    [Fact]
    public void GetStationArt_WithInvalidStation_ReturnsEmptyString()
    {
        // Arrange - This test assumes we could somehow pass an invalid station
        // Since Station is an enum, we'll test with a cast to invalid value
        var invalidStation = (Station)999;

        // Act
        var result = StationAsciiArt.GetStationArt(invalidStation, true);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetStationArt_AllFramesHaveConsistentStructure()
    {
        // Arrange
        var station = Station.TripleJ;
        var frames = new List<string>();

        // Act
        for (int i = 0; i < 4; i++)
        {
            frames.Add(StationAsciiArt.GetStationArt(station, true));
            StationAsciiArt.IncrementFrame();
        }

        // Assert
        foreach (var frame in frames)
        {
            frame.Should().NotBeNullOrEmpty();
            var lines = frame.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            lines.Should().HaveCount(6); // All frames should have 6 lines
            
            // Check that all lines have consistent width (box structure)
            foreach (var line in lines)
            {
                line.Should().StartWith("    "); // All lines should have same indentation
                line.Should().Contain("║"); // All lines should have side borders
            }
        }
    }

    [Fact]
    public void GetJaybirdArt_HasConsistentStructure()
    {
        // Act
        var result = StationAsciiArt.GetJaybirdArt();

        // Assert
        result.Should().NotBeNullOrEmpty();
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(6); // Should have 6 lines
        
        // Check that lines have consistent indentation pattern
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            line.Should().NotBeNullOrEmpty();
            
            // Check indentation pattern (should be centered)
            if (i == 0 || i == 4 || i == 5) // First, second to last, and last lines
            {
                line.Should().StartWith("   "); // 3 spaces
            }
            else if (i == 1 || i == 3) // Second and fourth lines
            {
                line.Should().StartWith(" "); // 1 space
            }
            else // Third line (middle)
            {
                line.Should().StartWith(" "); // 1 space
            }
        }
    }

    [Fact]
    public void GetPlayingIndicator_AllFramesReturnValidSymbols()
    {
        // Arrange
        var validSymbols = new[] { "▶", "▷", "⏸" };

        // Act & Assert
        for (int i = 0; i < 10; i++) // Test multiple cycles
        {
            var playingIndicator = StationAsciiArt.GetPlayingIndicator(true);
            playingIndicator.Should().BeOneOf(validSymbols);
            
            var notPlayingIndicator = StationAsciiArt.GetPlayingIndicator(false);
            notPlayingIndicator.Should().Be("⏸");
            
            StationAsciiArt.IncrementFrame();
        }
    }
}