using System.Drawing;
using ScrollShot.App.Models;
using ScrollShot.Capture;
using ScrollShot.Capture.Models;
using ScrollShot.Editor;
using ScrollShot.Editor.ViewModels;
using ScrollShot.Overlay;
using ScrollShot.Scroll;
using ScrollShot.Scroll.Models;
using Bitmap = System.Drawing.Bitmap;

namespace ScrollShot.App.Services;

public sealed class CaptureOrchestrator
{
    private readonly Func<AppSettings> _settingsProvider;
    private SelectionOverlayWindow? _activeOverlay;

    public CaptureOrchestrator(Func<AppSettings> settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    public Task BeginCaptureAsync()
    {
        if (_activeOverlay is not null)
        {
            return Task.CompletedTask;
        }

        var overlay = new SelectionOverlayWindow();
        _activeOverlay = overlay;
        CaptureController? controller = null;
        ScrollSession? session = null;

        overlay.InstantCaptureRequested += (_, args) =>
        {
            using var capturer = ScreenCapturerFactory.Create(args.Region);
            using var frame = capturer.CaptureFrame();
            if (frame is null)
            {
                return;
            }

            ShowEditor(CreateInstantCaptureResult(frame));
            overlay.Close();
        };

        overlay.ScrollCaptureStarted += async (_, args) =>
        {
            if (controller is null)
            {
                session = new ScrollSession();
                session.PreviewUpdated += overlay.UpdatePreview;
                controller = new CaptureController(ScreenCapturerFactory.Create(args.Region), session);
                controller.Start(args.Region, args.Direction ?? ScrollDirection.Vertical);
            }
        };

        overlay.ScrollStepRequested += async (_, _) =>
        {
            if (controller is not null)
            {
                await controller.CaptureAsync();
            }
        };

        overlay.CaptureCompleted += (_, _) =>
        {
            if (controller is not null)
            {
                var result = controller.Finish();
                ShowEditor(result);
            }

            overlay.Close();
        };

        overlay.Cancelled += (_, _) => overlay.Close();
        overlay.Closed += (_, _) =>
        {
            controller?.Dispose();
            _activeOverlay = null;
        };

        overlay.Show();
        return Task.CompletedTask;
    }

    private void ShowEditor(CaptureResult captureResult)
    {
        var settings = _settingsProvider();
        var viewModel = new PreviewEditorViewModel(captureResult, saveFolder: settings.SaveFolder);
        var window = new PreviewEditorWindow(viewModel);
        window.Show();
        window.Activate();
    }

    private static CaptureResult CreateInstantCaptureResult(CapturedFrame frame)
    {
        var bitmap = (Bitmap)frame.Bitmap.Clone();
        return new CaptureResult(
            new[] { new ScrollSegment(bitmap, 0) },
            new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, frame.Region.Width, frame.Region.Height)),
            ScrollDirection.Vertical,
            frame.Region.Width,
            frame.Region.Height,
            isScrollingCapture: false);
    }
}
