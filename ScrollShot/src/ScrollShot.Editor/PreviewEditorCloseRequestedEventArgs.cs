namespace ScrollShot.Editor;

public sealed class PreviewEditorCloseRequestedEventArgs : EventArgs
{
    public PreviewEditorCloseRequestedEventArgs(bool discardConfirmed)
    {
        DiscardConfirmed = discardConfirmed;
    }

    public bool DiscardConfirmed { get; }
}
