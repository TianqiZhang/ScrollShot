using FluentAssertions;
using ScrollShot.Editor.Models;

namespace ScrollShot.Editor.Tests.Models;

public sealed class EditStateTests
{
    [Fact]
    public void Default_UsesChromeAndNoEdits()
    {
        var state = EditState.Default;

        state.IncludeChrome.Should().BeTrue();
        state.TrimRange.Should().Be(new TrimRange(0, 0));
        state.CutRanges.Should().BeEmpty();
        state.CropRect.Should().BeNull();
    }

    [Fact]
    public void RecordInitialization_AllowsImmutableSnapshotStyle()
    {
        var state = EditState.Default with
        {
            TrimRange = new TrimRange(5, 8),
            CutRanges = new[] { new CutRange(10, 25) },
            CropRect = new CropRect(1, 2, 30, 40),
            IncludeChrome = false,
        };

        state.TrimRange.Should().Be(new TrimRange(5, 8));
        state.CutRanges.Should().ContainSingle().Which.Should().Be(new CutRange(10, 25));
        state.CropRect.Should().Be(new CropRect(1, 2, 30, 40));
        state.IncludeChrome.Should().BeFalse();
    }
}
