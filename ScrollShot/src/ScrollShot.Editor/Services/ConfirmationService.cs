using System.Windows;

namespace ScrollShot.Editor.Services;

public sealed class ConfirmationService : IConfirmationService
{
    public bool ConfirmDiscard()
    {
        return MessageBox.Show(
                   "Discard this capture and lose any unsaved edits?",
                   "Discard capture",
                   MessageBoxButton.YesNo,
                   MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }
}
