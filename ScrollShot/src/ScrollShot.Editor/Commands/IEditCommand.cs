using ScrollShot.Editor.Models;

namespace ScrollShot.Editor.Commands;

public interface IEditCommand
{
    string Description { get; }

    EditState Apply(EditState state);

    EditState Undo(EditState state);
}
