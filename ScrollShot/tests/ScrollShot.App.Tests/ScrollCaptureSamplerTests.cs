using FluentAssertions;
using ScrollShot.App.Services;

namespace ScrollShot.App.Tests;

public sealed class ScrollCaptureSamplerTests
{
    [Fact]
    public async Task Start_BeginsPeriodicSampling()
    {
        var tickCount = 0;
        var twoTicksObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sampler = new ScrollCaptureSampler(
            TimeSpan.FromMilliseconds(15),
            () =>
            {
                if (Interlocked.Increment(ref tickCount) >= 2)
                {
                    twoTicksObserved.TrySetResult();
                }

                return Task.CompletedTask;
            });

        sampler.Start();
        var completedTask = await Task.WhenAny(twoTicksObserved.Task, Task.Delay(500));
        await sampler.StopAsync();

        completedTask.Should().Be(twoTicksObserved.Task);
        tickCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task StopAsync_StopsFurtherSampling()
    {
        var tickCount = 0;
        var firstTickObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sampler = new ScrollCaptureSampler(
            TimeSpan.FromMilliseconds(15),
            () =>
            {
                if (Interlocked.Increment(ref tickCount) == 1)
                {
                    firstTickObserved.TrySetResult();
                }

                return Task.CompletedTask;
            });

        sampler.Start();
        var firstTickTask = await Task.WhenAny(firstTickObserved.Task, Task.Delay(500));
        firstTickTask.Should().Be(firstTickObserved.Task);

        await sampler.StopAsync();
        var tickCountAfterStop = tickCount;

        await Task.Delay(75);

        tickCount.Should().Be(tickCountAfterStop);
    }
}
