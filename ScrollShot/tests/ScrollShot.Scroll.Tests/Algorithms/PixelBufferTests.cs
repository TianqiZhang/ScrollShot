using FluentAssertions;
using ScrollShot.Scroll.Algorithms;

namespace ScrollShot.Scroll.Tests.Algorithms;

public sealed class PixelBufferTests
{
    [Fact]
    public void ExtractSubRectangle_ReturnsExpectedPixels()
    {
        var snapshot = new PixelBufferSnapshot(
            2,
            2,
            8,
            new byte[]
            {
                1, 2, 3, 4, 10, 20, 30, 40,
                5, 6, 7, 8, 50, 60, 70, 80,
            });

        var extracted = PixelBuffer.ExtractSubRectangle(snapshot, new System.Drawing.Rectangle(1, 0, 1, 2));

        extracted.Width.Should().Be(1);
        extracted.Height.Should().Be(2);
        extracted.Pixels.Should().Equal(10, 20, 30, 40, 50, 60, 70, 80);
    }

    [Fact]
    public void ComputeSumOfAbsoluteDifferences_ReturnsExpectedValue()
    {
        var value = PixelBuffer.ComputeSumOfAbsoluteDifferences(new byte[] { 0, 10, 20 }, new byte[] { 10, 10, 10 });

        value.Should().Be(20);
    }
}
