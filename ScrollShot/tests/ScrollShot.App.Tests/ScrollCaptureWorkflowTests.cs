using System.Drawing;
using FluentAssertions;
using ScrollShot.App.Services;
using ScrollShot.Capture.Models;
using ScrollShot.Scroll.Models;

namespace ScrollShot.App.Tests;

public sealed class ScrollCaptureWorkflowTests
{
    [Fact]
    public async Task CloseAsync_WaitsForQueuedCaptureBeforeDisposingController()
    {
        var controller = new FakeScrollCaptureController();
        var workflow = new ScrollCaptureWorkflow(controller);
        await workflow.StartAsync(new ScreenRect(0, 0, 3, 3), ScrollDirection.Vertical);

        var secondCaptureRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.NextCaptureTask = secondCaptureRelease.Task;

        var queuedCaptureTask = workflow.CaptureStepAsync();
        var closeTask = workflow.CloseAsync();

        controller.DisposeCount.Should().Be(0);

        secondCaptureRelease.SetResult();
        await Task.WhenAll(queuedCaptureTask, closeTask);

        controller.DisposeCount.Should().Be(1);
        controller.CaptureCount.Should().Be(2);
    }

    [Fact]
    public async Task CompleteAsync_AllowsRetryAfterInvalidResult()
    {
        var controller = new FakeScrollCaptureController
        {
            FinishException = new InvalidOperationException("At least two frames are required to build a scroll result."),
        };
        var workflow = new ScrollCaptureWorkflow(controller);
        await workflow.StartAsync(new ScreenRect(0, 0, 3, 3), ScrollDirection.Vertical);

        InvalidOperationException? observedException = null;
        await workflow.CompleteAsync(
            _ => Task.CompletedTask,
            exception =>
            {
                observedException = exception;
                controller.FinishException = null;
                return Task.CompletedTask;
            });

        await workflow.CaptureStepAsync();

        observedException.Should().NotBeNull();
        controller.CaptureCount.Should().Be(3);
    }

    [Fact]
    public async Task CaptureStepAsync_CoalescesBurstOfRequests()
    {
        var controller = new FakeScrollCaptureController();
        var workflow = new ScrollCaptureWorkflow(controller);
        await workflow.StartAsync(new ScreenRect(0, 0, 3, 3), ScrollDirection.Vertical);

        var releaseCapture = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.NextCaptureTask = releaseCapture.Task;

        var firstStep = workflow.CaptureStepAsync();
        var secondStep = workflow.CaptureStepAsync();
        var thirdStep = workflow.CaptureStepAsync();

        releaseCapture.SetResult();
        await Task.WhenAll(firstStep, secondStep, thirdStep);

        controller.CaptureCount.Should().Be(2);
    }

    private sealed class FakeScrollCaptureController : IScrollCaptureController
    {
        public Task NextCaptureTask { get; set; } = Task.CompletedTask;

        public int CaptureCount { get; private set; }

        public int DisposeCount { get; private set; }

        public InvalidOperationException? FinishException { get; set; }

        public void Start(ScreenRect region, ScrollDirection direction)
        {
        }

        public async Task<bool> CaptureAsync(CancellationToken cancellationToken = default)
        {
            CaptureCount++;
            var pendingCapture = NextCaptureTask;
            NextCaptureTask = Task.CompletedTask;
            await pendingCapture;
            return true;
        }

        public CaptureResult Finish()
        {
            if (FinishException is not null)
            {
                throw FinishException;
            }

            return new CaptureResult(
                new[] { new ScrollSegment(new Bitmap(1, 1), 0) },
                new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 1, 1)),
                ScrollDirection.Vertical,
                1,
                1,
                isScrollingCapture: true);
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
