using ScrollShot.Editor.Models;

namespace ScrollShot.Editor.Commands;

public sealed class TrimCommand : IEditCommand
{
    private TrimRange _previousRange;

    public TrimCommand(TrimRange trimRange)
    {
        TrimRange = trimRange;
    }

    public string Description => "Trim";

    public TrimRange TrimRange { get; }

    public EditState Apply(EditState state)
    {
        _previousRange = state.TrimRange;
        return state with { TrimRange = TrimRange };
    }

    public EditState Undo(EditState state)
    {
        return state with { TrimRange = _previousRange };
    }
}
