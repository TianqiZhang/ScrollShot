using FluentAssertions;
using ScrollShot.Scroll.Shared;

namespace ScrollShot.Scroll.Tests.Shared;

public sealed class RowColumnHashTests
{
    [Fact]
    public void ComputeRowHashes_ReturnsExpectedSums()
    {
        var pixels = new byte[]
        {
            1, 2, 3, 4, 10, 20, 30, 40,
            5, 6, 7, 8, 50, 60, 70, 80,
        };

        var hashes = RowColumnHash.ComputeRowHashes(pixels, width: 2, height: 2, stride: 8);

        hashes.Should().Equal(110, 286);
    }

    [Fact]
    public void ComputeColumnHashes_ReturnsExpectedSums()
    {
        var pixels = new byte[]
        {
            1, 2, 3, 4, 10, 20, 30, 40,
            5, 6, 7, 8, 50, 60, 70, 80,
        };

        var hashes = RowColumnHash.ComputeColumnHashes(pixels, width: 2, height: 2, stride: 8);

        hashes.Should().Equal(36, 360);
    }

    [Fact]
    public void RowDifference_IsZeroForIdenticalRows()
    {
        var row = new byte[] { 10, 20, 30, 40 };

        var difference = RowColumnHash.RowDifference(row, row);

        difference.Should().Be(0);
    }
}
