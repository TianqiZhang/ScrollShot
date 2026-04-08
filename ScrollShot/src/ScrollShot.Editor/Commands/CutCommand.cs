using ScrollShot.Editor.Models;

namespace ScrollShot.Editor.Commands;

public sealed class CutCommand : IEditCommand
{
    private IReadOnlyList<CutRange> _previousRanges = Array.Empty<CutRange>();

    public CutCommand(CutRange cutRange)
    {
        CutRange = cutRange;
    }

    public string Description => "Cut";

    public CutRange CutRange { get; }

    public EditState Apply(EditState state)
    {
        if (state.CutRanges.Any(existing => RangesOverlap(existing, CutRange)))
        {
            throw new InvalidOperationException("Cut ranges cannot overlap.");
        }

        _previousRanges = state.CutRanges.ToArray();
        var updatedRanges = state.CutRanges.Append(CutRange).OrderBy(range => range.StartPixel).ToArray();
        return state with { CutRanges = updatedRanges };
    }

    public EditState Undo(EditState state)
    {
        return state with { CutRanges = _previousRanges };
    }

    private static bool RangesOverlap(CutRange left, CutRange right)
    {
        return left.StartPixel < right.EndPixel && right.StartPixel < left.EndPixel;
    }
}
