using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using ScrollShot.Overlay.Helpers;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Overlay.Controls;

public partial class LivePreviewStrip : System.Windows.Controls.UserControl
{
    public LivePreviewStrip()
    {
        InitializeComponent();
    }

    public event EventHandler? DoneClicked;

    public void SetInputPassThroughMode(bool enabled)
    {
        DoneButton.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        CompletionHintTextBlock.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetPreview(Bitmap bitmap, ScrollDirection direction)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        PreviewImage.Source = BitmapConversion.ToBitmapSource(bitmap);
        DirectionTextBlock.Text = direction == ScrollDirection.Vertical ? "↓" : "→";

        if (direction == ScrollDirection.Vertical)
        {
            Width = 180;
            Height = 300;
            PreviewImage.Width = 150;
            PreviewImage.Height = 220;
        }
        else
        {
            Width = 340;
            Height = 150;
            PreviewImage.Width = 300;
            PreviewImage.Height = 80;
        }
    }

    private void OnDoneButtonClick(object sender, RoutedEventArgs e)
    {
        DoneClicked?.Invoke(this, EventArgs.Empty);
    }
}
