using System.Drawing;
using FluentAssertions;
using ScrollShot.Capture.Models;
using ScrollShot.Editor.Composition;
using ScrollShot.Editor.Models;
using ScrollShot.Editor.Services;
using ScrollShot.Editor.ViewModels;
using ScrollShot.Scroll.Models;

namespace ScrollShot.Editor.Tests.ViewModels;

public sealed class PreviewEditorViewModelTests
{
    [Fact]
    public void SaveCommand_UsesTimestampedPngName()
    {
        var fileService = new FakeImageFileService();
        var viewModel = CreateViewModel(imageFileService: fileService, nowProvider: () => new DateTimeOffset(2026, 4, 8, 2, 0, 0, TimeSpan.Zero));

        viewModel.SaveCommand.Execute(null);

        fileService.SavedPath.Should().EndWith("ScrollShot_20260408_020000.png");
        viewModel.LastSavedPath.Should().Be(fileService.SavedPath);
    }

    [Fact]
    public void CopyCommand_SendsImageToClipboardService()
    {
        var clipboard = new FakeClipboardService();
        var viewModel = CreateViewModel(clipboardService: clipboard);

        viewModel.CopyCommand.Execute(null);

        clipboard.CopyCount.Should().Be(1);
    }

    [Fact]
    public void DiscardCommand_RespectsConfirmationWhenDirty()
    {
        var confirmation = new FakeConfirmationService(confirmDiscard: false);
        var viewModel = CreateViewModel(confirmationService: confirmation);
        var closeRequested = false;
        viewModel.CloseRequested += (_, _) => closeRequested = true;
        viewModel.ApplyTrim(new TrimRange(2, 0));

        viewModel.DiscardCommand.Execute(null);

        closeRequested.Should().BeFalse();
    }

    private static PreviewEditorViewModel CreateViewModel(
        IClipboardService? clipboardService = null,
        IImageFileService? imageFileService = null,
        IConfirmationService? confirmationService = null,
        Func<DateTimeOffset>? nowProvider = null)
    {
        using var segment = CreateBitmap(3, 6, Color.Red);
        var result = new CaptureResult(
            new[] { new ScrollSegment((Bitmap)segment.Clone(), 0) },
            new ZoneLayout(0, 0, 0, 0, new ScreenRect(0, 0, 3, 6)),
            ScrollDirection.Vertical,
            3,
            6);

        return new PreviewEditorViewModel(
            result,
            new ImageCompositor(),
            clipboardService,
            imageFileService,
            confirmationService,
            @"C:\captures",
            nowProvider);
    }

    private static Bitmap CreateBitmap(int width, int height, Color color)
    {
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public int CopyCount { get; private set; }

        public void SetImage(Bitmap bitmap)
        {
            CopyCount++;
        }
    }

    private sealed class FakeImageFileService : IImageFileService
    {
        public string? SavedPath { get; private set; }

        public void SavePng(Bitmap bitmap, string path)
        {
            SavedPath = path;
        }
    }

    private sealed class FakeConfirmationService : IConfirmationService
    {
        private readonly bool _confirmDiscard;

        public FakeConfirmationService(bool confirmDiscard)
        {
            _confirmDiscard = confirmDiscard;
        }

        public bool ConfirmDiscard() => _confirmDiscard;
    }
}
