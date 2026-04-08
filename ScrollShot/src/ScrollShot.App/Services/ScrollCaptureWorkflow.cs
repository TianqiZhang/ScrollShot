using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.App.Services;

internal sealed class ScrollCaptureWorkflow
{
    private readonly IScrollCaptureController _controller;
    private readonly object _queueLock = new();
    private readonly CancellationTokenSource _captureCancellation = new();
    private Task _captureQueue = Task.CompletedTask;
    private bool _isCompleting;
    private bool _isClosing;

    public ScrollCaptureWorkflow(IScrollCaptureController controller)
    {
        _controller = controller;
    }

    public Task StartAsync(ScreenRect region, ScrollDirection direction)
    {
        _controller.Start(region, direction);
        return EnqueueCaptureAsync((controller, cancellationToken) => controller.CaptureAsync(cancellationToken));
    }

    public Task CaptureStepAsync()
    {
        if (_isClosing || _isCompleting)
        {
            return _captureQueue;
        }

        return EnqueueCaptureAsync((controller, cancellationToken) => controller.CaptureAsync(cancellationToken));
    }

    public Task CompleteAsync(Func<CaptureResult, Task> onCompleted, Func<InvalidOperationException, Task> onInvalidOperation)
    {
        ArgumentNullException.ThrowIfNull(onCompleted);
        ArgumentNullException.ThrowIfNull(onInvalidOperation);

        if (_isClosing || _isCompleting)
        {
            return _captureQueue;
        }

        _isCompleting = true;
        return EnqueueCaptureAsync(async (controller, cancellationToken) =>
        {
            await controller.CaptureAsync(cancellationToken);

            try
            {
                var result = controller.Finish();
                await onCompleted(result);
            }
            catch (InvalidOperationException exception)
            {
                _isCompleting = false;
                await onInvalidOperation(exception);
            }
        });
    }

    public void Cancel()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        _captureCancellation.Cancel();
    }

    public Task CloseAsync()
    {
        Cancel();
        lock (_queueLock)
        {
            _captureQueue = _captureQueue.ContinueWith(_ =>
            {
                _controller.Dispose();
                _captureCancellation.Dispose();
            }, TaskScheduler.Default);

            return _captureQueue;
        }
    }

    private Task EnqueueCaptureAsync(Func<IScrollCaptureController, CancellationToken, Task> work)
    {
        lock (_queueLock)
        {
            _captureQueue = _captureQueue.ContinueWith(
                async _ =>
                {
                    if (_captureCancellation.IsCancellationRequested)
                    {
                        return;
                    }

                    try
                    {
                        await work(_controller, _captureCancellation.Token);
                    }
                    catch (OperationCanceledException) when (_captureCancellation.IsCancellationRequested)
                    {
                    }
                    catch (ObjectDisposedException) when (_isClosing)
                    {
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default).Unwrap();

            return _captureQueue;
        }
    }
}
