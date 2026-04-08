using ScrollShot.Editor.Models;

namespace ScrollShot.Editor.Commands;

public sealed class ChromeToggleCommand : IEditCommand
{
    private bool _previousIncludeChrome;

    public ChromeToggleCommand(bool includeChrome)
    {
        IncludeChrome = includeChrome;
    }

    public string Description => "Chrome toggle";

    public bool IncludeChrome { get; }

    public EditState Apply(EditState state)
    {
        _previousIncludeChrome = state.IncludeChrome;
        return state with { IncludeChrome = IncludeChrome };
    }

    public EditState Undo(EditState state)
    {
        return state with { IncludeChrome = _previousIncludeChrome };
    }
}
