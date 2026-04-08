using ScrollShot.Editor.Models;

namespace ScrollShot.Editor.Commands;

public sealed class EditCommandStack
{
    private readonly Stack<IEditCommand> _undoStack = new();
    private readonly Stack<IEditCommand> _redoStack = new();

    public EditCommandStack(EditState? initialState = null)
    {
        CurrentState = initialState ?? EditState.Default;
    }

    public EditState CurrentState { get; private set; }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public EditState Apply(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        CurrentState = command.Apply(CurrentState);
        _undoStack.Push(command);
        _redoStack.Clear();
        return CurrentState;
    }

    public EditState Undo()
    {
        if (!CanUndo)
        {
            return CurrentState;
        }

        var command = _undoStack.Pop();
        CurrentState = command.Undo(CurrentState);
        _redoStack.Push(command);
        return CurrentState;
    }

    public EditState Redo()
    {
        if (!CanRedo)
        {
            return CurrentState;
        }

        var command = _redoStack.Pop();
        CurrentState = command.Apply(CurrentState);
        _undoStack.Push(command);
        return CurrentState;
    }
}
