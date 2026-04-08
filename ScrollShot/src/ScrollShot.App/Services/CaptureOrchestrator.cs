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
    private readonly object _captureQueueLock = new();

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
        var captureCancellation = new CancellationTokenSource();
        var captureQueue = Task.CompletedTask;
        var isCompleting = false;
        var isClosing = false;

        Task EnqueueCaptureAsync(Func<CaptureController, CancellationToken, Task> work)
        {
            lock (_captureQueueLock)
            {
                captureQueue = captureQueue.ContinueWith(
                    async _ =>
                    {
                        if (captureCancellation.IsCancellationRequested)
                        {
                            return;
                        }

                        var activeController = controller;
                        if (activeController is null)
                        {
                            return;
                        }

                        try
                        {
                            await work(activeController, captureCancellation.Token);
                        }
                        catch (OperationCanceledException) when (captureCancellation.IsCancellationRequested)
                        {
                        }
                        catch (ObjectDisposedException) when (isClosing)
                        {
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default).Unwrap();

                return captureQueue;
            }
        }

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
            if (controller is null && !isClosing)
            {
                session = new ScrollSession();
                session.PreviewUpdated += overlay.UpdatePreview;
                controller = new CaptureController(ScreenCapturerFactory.Create(args.Region), session);
                controller.Start(args.Region, args.Direction ?? ScrollDirection.Vertical);
                await EnqueueCaptureAsync((activeController, cancellationToken) => activeController.CaptureAsync(cancellationToken));
            }
        };

        overlay.ScrollStepRequested += (_, _) =>
        {
            if (controller is not null && !isClosing && !isCompleting)
            {
                _ = EnqueueCaptureAsync((activeController, cancellationToken) => activeController.CaptureAsync(cancellationToken));
            }
        };

        overlay.CaptureCompleted += (_, _) =>
        {
            if (controller is null || isClosing || isCompleting)
            {
                return;
            }

            isCompleting = true;
            _ = EnqueueCaptureAsync(async (activeController, cancellationToken) =>
            {
                await activeController.CaptureAsync(cancellationToken);

                try
                {
                    var result = activeController.Finish();
                    await overlay.Dispatcher.InvokeAsync(() =>
                    {
                        ShowEditor(result);
                        isClosing = true;
                        overlay.Close();
                    });
                }
                catch (InvalidOperationException exception)
                {
                    await overlay.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show(
                            exception.Message,
                            "ScrollShot",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        isCompleting = false;
                    });
                }
            });
        };

        overlay.Cancelled += (_, _) =>
        {
            isClosing = true;
            captureCancellation.Cancel();
            overlay.Close();
        };
        overlay.Closed += (_, _) =>
        {
            isClosing = true;
            captureCancellation.Cancel();
            var controllerToDispose = controller;
            controller = null;
            _ = captureQueue.ContinueWith(_ =>
            {
                controllerToDispose?.Dispose();
                captureCancellation.Dispose();
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
