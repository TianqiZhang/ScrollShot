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
            ? "Capture while scrolling down"
            : "Capture side to side";
        StatusTextBlock.Text = direction == ScrollDirection.Vertical
            ? "Keep scrolling down. When the preview looks complete, click Finish."
            : "Keep scrolling sideways. When the preview looks complete, click Finish.";

        if (direction == ScrollDirection.Vertical)
        {
            Width = 232;
            Height = 356;
            PreviewImage.Width = 184;
            PreviewImage.Height = 236;
        }
        else
        {
            Width = 360;
            Height = 208;
            PreviewImage.Width = 312;
            PreviewImage.Height = 112;
        }
    }

    private void OnDoneButtonClick(object sender, RoutedEventArgs e)
    {
        DoneClicked?.Invoke(this, EventArgs.Empty);
    }
}
