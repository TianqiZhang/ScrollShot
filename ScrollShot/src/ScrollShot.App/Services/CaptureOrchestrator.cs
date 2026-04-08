using System.Drawing;
using System.Windows;
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
        ScrollSession? session = null;
        ScrollCaptureWorkflow? workflow = null;
        var overlayClosed = false;
        CancellationTokenSource? samplingCancellation = null;
        Task? samplingTask = null;

        overlay.InstantCaptureRequested += (_, args) =>
        {
            overlay.Hide();
            using var capturer = ScreenCapturerFactory.Create(args.Region);
            using var frame = capturer.CaptureFrame();
            if (frame is null)
            {
                overlay.Close();
                return;
            }

            ShowEditor(CreateInstantCaptureResult(frame));
            overlay.Close();
        };

        overlay.ScrollCaptureStarted += async (_, args) =>
        {
            if (workflow is null)
            {
                session = new ScrollSession();
                session.PreviewUpdated += overlay.UpdatePreview;
                var shouldSuspendOverlayDuringCapture = !overlay.IsExcludedFromCapture;
                workflow = new ScrollCaptureWorkflow(
                    new ScrollCaptureControllerAdapter(
                        new CaptureController(
                            ScreenCapturerFactory.Create(args.Region),
                            session),
                        shouldSuspendOverlayDuringCapture
                            ? () => overlay.Dispatcher.InvokeAsync(() =>
                            {
                                if (!overlayClosed && overlay.IsVisible)
                                {
                                    overlay.Hide();
                                }
                            }).Task
                            : null,
                        shouldSuspendOverlayDuringCapture
                            ? () => overlay.Dispatcher.InvokeAsync(() =>
                            {
                                if (!overlayClosed && !overlay.IsVisible)
                                {
                                    overlay.Show();
                                }
                            }).Task
                            : null));

                await workflow.StartAsync(args.Region, args.Direction ?? ScrollDirection.Vertical);

                samplingCancellation = new CancellationTokenSource();
                samplingTask = Task.Run(async () =>
                {
                    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(120));
                    try
                    {
                        while (await timer.WaitForNextTickAsync(samplingCancellation.Token))
                        {
                            if (workflow is null)
                            {
                                continue;
                            }

                            await workflow.CaptureStepAsync();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                });
            }
        };

        overlay.CaptureCompleted += (_, _) =>
        {
            if (workflow is null)
            {
                return;
            }

            _ = workflow.CompleteAsync(
                result =>
                {
                    return overlay.Dispatcher.InvokeAsync(() =>
                    {
                        ShowEditor(result);
                        overlay.Close();
                    }).Task;
                },
                exception =>
                {
                    return overlay.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show(
                            exception.Message,
                            "ScrollShot",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }).Task;
                });
        };

        overlay.Cancelled += (_, _) =>
        {
            samplingCancellation?.Cancel();
            workflow?.Cancel();
            overlay.Close();
        };
        overlay.Closed += (_, _) =>
        {
            overlayClosed = true;
            samplingCancellation?.Cancel();
            var workflowToClose = workflow;
            workflow = null;
            _ = workflowToClose?.CloseAsync();
            _ = samplingTask?.ContinueWith(_ =>
            {
                samplingCancellation?.Dispose();
            }, TaskScheduler.Default);
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
