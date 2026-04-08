using FluentAssertions;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Scroll.Tests.Models;

public sealed class ZoneLayoutTests
{
    [Fact]
    public void Constructor_SetsMarginsAndBand()
    {
        var layout = new ZoneLayout(50, 20, 5, 7, new ScreenRect(5, 50, 100, 300));

        layout.FixedTop.Should().Be(50);
        layout.FixedBottom.Should().Be(20);
        layout.FixedLeft.Should().Be(5);
        layout.FixedRight.Should().Be(7);
        layout.ScrollBand.Should().Be(new ScreenRect(5, 50, 100, 300));
    }

    [Fact]
    public void Constructor_RejectsNegativeMargins()
    {
        var action = () => new ZoneLayout(-1, 0, 0, 0, new ScreenRect(0, 0, 10, 10));

        action.Should().Throw<ArgumentOutOfRangeException>();
    }
}
