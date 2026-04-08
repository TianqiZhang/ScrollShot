using System.Drawing;
using System.Drawing.Imaging;
using ScrollShot.Capture.Models;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Bitmap = System.Drawing.Bitmap;

namespace ScrollShot.Capture;

public sealed class DxgiScreenCapturer : IScreenCapturer
{
    private readonly object _sync = new();
    private readonly GdiScreenCapturer _gdiFallback = new();
    private ID3D11Device? _device;
    private IDXGIOutputDuplication? _duplication;
    private ID3D11Texture2D? _stagingTexture;
    private ScreenRect? _region;
    private Rectangle _outputBounds;
    private bool _useFallback = true;

    public bool IsAvailable { get; private set; } = OperatingSystem.IsWindows();

    public void Initialize(ScreenRect region)
    {
        _region = region;
        _gdiFallback.Initialize(region);

        lock (_sync)
        {
            DisposeDxgiResources();
            _useFallback = !TryInitializeDxgi(region);
        }
    }

    public CapturedFrame? CaptureFrame()
    {
        if (_region is null)
        {
            throw new InvalidOperationException("The capturer must be initialized before capturing.");
        }

        if (_useFallback)
        {
            return _gdiFallback.CaptureFrame();
        }

        lock (_sync)
        {
            IDXGIResource? desktopResource = null;
            var frameAcquired = false;

            try
            {
                if (_duplication is null || _device is null)
                {
                    _useFallback = true;
                    return _gdiFallback.CaptureFrame();
                }

                var acquireResult = _duplication.AcquireNextFrame(50, out _, out desktopResource);
                if (acquireResult == Vortice.DXGI.ResultCode.WaitTimeout)
                {
                    return null;
                }

                if (acquireResult == Vortice.DXGI.ResultCode.AccessLost || acquireResult == Vortice.DXGI.ResultCode.DeviceRemoved)
                {
                    _useFallback = !TryInitializeDxgi(_region.Value);
                    return _useFallback ? _gdiFallback.CaptureFrame() : null;
                }

                acquireResult.CheckError();
                frameAcquired = true;

                using var screenTexture = desktopResource.QueryInterface<ID3D11Texture2D>();
                EnsureStagingTexture(_region.Value);

                var region = _region.Value;
                var relativeLeft = region.X - _outputBounds.Left;
                var relativeTop = region.Y - _outputBounds.Top;
                var sourceBox = new Box(
                    relativeLeft,
                    relativeTop,
                    0,
                    relativeLeft + region.Width,
                    relativeTop + region.Height,
                    1);

                _device.ImmediateContext.CopySubresourceRegion(
                    _stagingTexture!,
                    0,
                    0,
                    0,
                    0,
                    screenTexture,
                    0,
                    sourceBox);

                var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
                var bounds = new Rectangle(0, 0, region.Width, region.Height);
                var destinationBits = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, bitmap.PixelFormat);

                try
                {
                    var mapped = _device.ImmediateContext.Map(_stagingTexture!, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);

                    try
                    {
                        unsafe
                        {
                            var source = (byte*)mapped.DataPointer;
                            var destination = (byte*)destinationBits.Scan0;
                            var sourceStride = (int)mapped.RowPitch;
                            var destinationStride = destinationBits.Stride;
                            var bytesPerRow = region.Width * 4;

                            for (var row = 0; row < region.Height; row++)
                            {
                                Buffer.MemoryCopy(source, destination, destinationStride, bytesPerRow);
                                source += sourceStride;
                                destination += destinationStride;
                            }
                        }
                    }
                    finally
                    {
                        _device.ImmediateContext.Unmap(_stagingTexture!, 0);
                    }
                }
                finally
                {
                    bitmap.UnlockBits(destinationBits);
                }

                return new CapturedFrame(bitmap, region, DateTimeOffset.UtcNow);
            }
            finally
            {
                desktopResource?.Dispose();

                if (frameAcquired)
                {
                    try
                    {
                        _duplication?.ReleaseFrame();
                    }
                    catch
                    {
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            DisposeDxgiResources();
            _gdiFallback.Dispose();
        }
    }

    private bool TryInitializeDxgi(ScreenRect region)
    {
        if (!IsAvailable)
        {
            return false;
        }

        try
        {
            if (!DxgiHelper.TryFindSingleOutputForRegion(region, out var adapter, out var output, out var outputBounds) ||
                adapter is null ||
                output is null)
            {
                return false;
            }

            using (adapter)
            using (output)
            {
                var featureLevels = new[]
                {
                    FeatureLevel.Level_11_1,
                    FeatureLevel.Level_11_0,
                };

                var result = D3D11.D3D11CreateDevice(
                    adapter,
                    DriverType.Unknown,
                    DeviceCreationFlags.None,
                    featureLevels,
                    out _device);

                if (result.Failure || _device is null)
                {
                    result = D3D11.D3D11CreateDevice(
                        adapter,
                        DriverType.Unknown,
                        DeviceCreationFlags.None,
                        null,
                        out _device);
                }

                if (result.Failure || _device is null)
                {
                    return false;
                }

                _duplication = output.DuplicateOutput(_device);
                _outputBounds = outputBounds;
                return true;
            }
        }
        catch
        {
            DisposeDxgiResources();
            return false;
        }
    }

    private void EnsureStagingTexture(ScreenRect region)
    {
        if (_device is null)
        {
            throw new InvalidOperationException("The DXGI device has not been initialized.");
        }

        if (_stagingTexture is not null &&
            _stagingTexture.Description.Width == region.Width &&
            _stagingTexture.Description.Height == region.Height)
        {
            return;
        }

        _stagingTexture?.Dispose();
        _stagingTexture = _device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)region.Width,
            Height = (uint)region.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            CPUAccessFlags = CpuAccessFlags.Read,
            BindFlags = BindFlags.None,
        });
    }

    private void DisposeDxgiResources()
    {
        _stagingTexture?.Dispose();
        _duplication?.Dispose();
        _device?.Dispose();

        _stagingTexture = null;
        _duplication = null;
        _device = null;
        _outputBounds = Rectangle.Empty;
    }
}
