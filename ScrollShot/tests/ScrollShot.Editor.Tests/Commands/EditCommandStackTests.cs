using FluentAssertions;
using ScrollShot.Editor.Commands;
using ScrollShot.Editor.Models;

namespace ScrollShot.Editor.Tests.Commands;

public sealed class EditCommandStackTests
{
    [Fact]
    public void ApplyUndoRedo_TracksStateTransitions()
    {
        var stack = new EditCommandStack();

        stack.Apply(new TrimCommand(new TrimRange(5, 10)));
        stack.Apply(new CutCommand(new CutRange(20, 30)));
        stack.Apply(new ChromeToggleCommand(false));

        stack.CurrentState.TrimRange.Should().Be(new TrimRange(5, 10));
        stack.CurrentState.CutRanges.Should().ContainSingle().Which.Should().Be(new CutRange(20, 30));
        stack.CurrentState.IncludeChrome.Should().BeFalse();

        stack.Undo();
        stack.CurrentState.IncludeChrome.Should().BeTrue();

        stack.Undo();
        stack.CurrentState.CutRanges.Should().BeEmpty();

        stack.Redo();
        stack.CurrentState.CutRanges.Should().ContainSingle();
    }

    [Fact]
    public void CutCommand_RejectsOverlappingCuts()
    {
        var stack = new EditCommandStack();
        stack.Apply(new CutCommand(new CutRange(10, 20)));

        var action = () => stack.Apply(new CutCommand(new CutRange(15, 25)));

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Undo_WhenEmpty_ReturnsCurrentState()
    {
        var stack = new EditCommandStack();
        var before = stack.CurrentState;

        var after = stack.Undo();

        after.Should().Be(before);
        stack.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void Redo_WhenEmpty_ReturnsCurrentState()
    {
        var stack = new EditCommandStack();
        var before = stack.CurrentState;

        var after = stack.Redo();

        after.Should().Be(before);
        stack.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Apply_ClearsRedoStack()
    {
        var stack = new EditCommandStack();
        stack.Apply(new TrimCommand(new TrimRange(5, 0)));
        stack.Apply(new ChromeToggleCommand(false));
        stack.Undo(); // chrome toggle is now in redo
        stack.CanRedo.Should().BeTrue();

        stack.Apply(new CropCommand(new CropRect(0, 0, 1, 1)));

        stack.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void FullUndoReturnsToDefaultState()
    {
        var stack = new EditCommandStack();
        stack.Apply(new TrimCommand(new TrimRange(5, 10)));
        stack.Apply(new ChromeToggleCommand(false));
        stack.Apply(new CutCommand(new CutRange(20, 30)));

        stack.Undo();
        stack.Undo();
        stack.Undo();

        stack.CurrentState.Should().Be(EditState.Default);
        stack.CanUndo.Should().BeFalse();
    }
}
