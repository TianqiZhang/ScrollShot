using FluentAssertions;
using ScrollShot.Capture.Models;

namespace ScrollShot.Capture.Tests.Models;

public sealed class ScreenRectTests
{
    [Fact]
    public void Constructor_SetsBounds()
    {
        var rect = new ScreenRect(10, 20, 30, 40);

        rect.X.Should().Be(10);
        rect.Y.Should().Be(20);
        rect.Width.Should().Be(30);
        rect.Height.Should().Be(40);
        rect.Right.Should().Be(40);
        rect.Bottom.Should().Be(60);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    public void Constructor_RejectsNonPositiveDimensions(int width, int height)
    {
        var action = () => new ScreenRect(0, 0, width, height);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }
}
