using System.Drawing;
using System.Windows;
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

    public void SetPreview(Bitmap bitmap, ScrollDirection direction)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        PreviewImage.Source = BitmapConversion.ToBitmapSource(bitmap);
        DirectionTextBlock.Text = direction == ScrollDirection.Vertical
            ? "Vertical scrolling capture"
            : "Horizontal scrolling capture";
        StatusTextBlock.Text = direction == ScrollDirection.Vertical
            ? "Scroll the target window normally. Finish once the live preview covers everything you want."
            : "Scroll the target window sideways. Finish once the preview covers the full horizontal range.";

        if (direction == ScrollDirection.Vertical)
        {
            Width = 260;
            Height = 420;
            PreviewImage.Width = 220;
            PreviewImage.Height = 280;
        }
        else
        {
            Width = 420;
            Height = 250;
            PreviewImage.Width = 360;
            PreviewImage.Height = 130;
        }
    }

    private void OnDoneButtonClick(object sender, RoutedEventArgs e)
    {
        DoneClicked?.Invoke(this, EventArgs.Empty);
    }
}
