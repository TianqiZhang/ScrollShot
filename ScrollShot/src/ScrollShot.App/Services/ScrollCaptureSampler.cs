namespace ScrollShot.App.Services;

internal sealed class ScrollCaptureSampler
{
    private readonly TimeSpan _interval;
    private readonly Func<Task> _onTickAsync;
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _cancellation;
    private Task? _loopTask;

    public ScrollCaptureSampler(TimeSpan interval, Func<Task> onTickAsync)
    {
        _interval = interval;
        _onTickAsync = onTickAsync ?? throw new ArgumentNullException(nameof(onTickAsync));
    }

    public void Start()
    {
        lock (_syncRoot)
        {
            if (_loopTask is not null)
            {
                return;
            }

            _cancellation = new CancellationTokenSource();
            _loopTask = RunAsync(_cancellation.Token);
        }
    }

    public async Task StopAsync()
    {
        Task? loopTask;
        CancellationTokenSource? cancellation;

        lock (_syncRoot)
        {
            loopTask = _loopTask;
            cancellation = _cancellation;
            _loopTask = null;
            _cancellation = null;
        }

        if (loopTask is null || cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        try
        {
            await loopTask;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await _onTickAsync();
        }
    }
}
