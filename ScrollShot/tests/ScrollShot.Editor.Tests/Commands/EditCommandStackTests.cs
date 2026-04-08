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
}
