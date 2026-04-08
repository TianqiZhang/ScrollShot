using System.Drawing;
using System.IO;
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
    private static readonly TimeSpan ScrollCaptureSamplingInterval = TimeSpan.FromMilliseconds(120);
    private readonly Func<AppSettings> _settingsProvider;
    private readonly IScrollSessionFactory _scrollSessionFactory;
    private SelectionOverlayWindow? _activeOverlay;

    public CaptureOrchestrator(Func<AppSettings> settingsProvider, IScrollSessionFactory? scrollSessionFactory = null)
    {
        _settingsProvider = settingsProvider;
        _scrollSessionFactory = scrollSessionFactory ?? new ScrollSessionFactory();
    }

    public Task BeginCaptureAsync()
    {
        if (_activeOverlay is not null)
        {
            return Task.CompletedTask;
        }

        var overlay = new SelectionOverlayWindow();
        _activeOverlay = overlay;
        IScrollSession? session = null;
        ScrollCaptureWorkflow? workflow = null;
        ScrollCaptureSampler? sampler = null;
        ScrollCaptureDebugDumpSession? debugDumpSession = null;
        var debugDumpWarningShown = false;
        var overlayClosed = false;

        void ShowDebugDumpWarningIfNeeded()
        {
            if (debugDumpWarningShown || string.IsNullOrWhiteSpace(debugDumpSession?.FailureMessage))
            {
                return;
            }

            debugDumpWarningShown = true;
            MessageBox.Show(
                debugDumpSession.FailureMessage,
                "ScrollShot Debug Dump",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

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
                var direction = args.Direction ?? ScrollDirection.Vertical;
                session = _scrollSessionFactory.CreateSession();
                session.PreviewUpdated += overlay.UpdatePreview;
                debugDumpSession = TryCreateDebugDumpSession(_settingsProvider(), args.Region, direction);
                var shouldSuspendOverlayDuringCapture = !overlay.IsExcludedFromCapture;
                workflow = new ScrollCaptureWorkflow(
                    new ScrollCaptureControllerAdapter(
                        new CaptureController(
                            CreateScrollCapturer(args.Region, debugDumpSession),
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

                await workflow.StartAsync(args.Region, direction);

                sampler = new ScrollCaptureSampler(
                    ScrollCaptureSamplingInterval,
                    () =>
                    {
                        var activeWorkflow = workflow;
                        return activeWorkflow is null
                            ? Task.CompletedTask
                            : activeWorkflow.CaptureStepAsync();
                    });
                sampler.Start();
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
                        debugDumpSession?.Complete(result);
                        ShowDebugDumpWarningIfNeeded();
                        ShowEditor(result);
                        overlay.Close();
                    }).Task;
                },
                exception =>
                {
                    return overlay.Dispatcher.InvokeAsync(() =>
                    {
                        ShowDebugDumpWarningIfNeeded();
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
            workflow?.Cancel();
            debugDumpSession?.Cancel("cancelled");
            ShowDebugDumpWarningIfNeeded();
            overlay.Close();
        };
        overlay.Closed += (_, _) =>
        {
            overlayClosed = true;
            var workflowToClose = workflow;
            workflow = null;
            _ = workflowToClose?.CloseAsync();
            var samplerToStop = sampler;
            sampler = null;
            _ = samplerToStop?.StopAsync();
            debugDumpSession?.Dispose();
            ShowDebugDumpWarningIfNeeded();
            debugDumpSession = null;
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

    private static IScreenCapturer CreateScrollCapturer(ScreenRect region, ScrollCaptureDebugDumpSession? debugDumpSession)
    {
        var capturer = ScreenCapturerFactory.Create(region);
        return debugDumpSession is null ? capturer : new RecordingScreenCapturer(capturer, debugDumpSession);
    }

    private static ScrollCaptureDebugDumpSession? TryCreateDebugDumpSession(
        AppSettings settings,
        ScreenRect region,
        ScrollDirection direction)
    {
        if (!settings.ScrollCaptureDebugDumpEnabled)
        {
            return null;
        }

        try
        {
            return ScrollCaptureDebugDumpSession.Create(
                settings.DebugDumpFolder,
                region,
                direction,
                ScrollCaptureSamplingInterval);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(
                $"Scroll capture debug dump could not be started.\n\n{exception.Message}",
                "ScrollShot Debug Dump",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return null;
        }
    }
}
