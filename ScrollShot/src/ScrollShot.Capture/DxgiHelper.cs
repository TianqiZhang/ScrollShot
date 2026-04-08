using System.Drawing;
using ScrollShot.Capture.Models;
using Vortice.DXGI;

namespace ScrollShot.Capture;

internal static class DxgiHelper
{
    public static bool TryFindSingleOutputForRegion(
        ScreenRect region,
        out IDXGIAdapter1? adapter,
        out IDXGIOutput1? output,
        out Rectangle outputBounds)
    {
        adapter = null;
        output = null;
        outputBounds = Rectangle.Empty;

        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        var targetRect = new Rectangle(region.X, region.Y, region.Width, region.Height);

        for (uint adapterIndex = 0; factory.EnumAdapters1(adapterIndex, out var currentAdapter).Success; adapterIndex++)
        {
            for (uint outputIndex = 0; currentAdapter.EnumOutputs(outputIndex, out var currentOutput).Success; outputIndex++)
            {
                var currentOutput1 = currentOutput.QueryInterface<IDXGIOutput1>();
                currentOutput.Dispose();

                var description = currentOutput1.Description;
                var bounds = Rectangle.FromLTRB(
                    description.DesktopCoordinates.Left,
                    description.DesktopCoordinates.Top,
                    description.DesktopCoordinates.Right,
                    description.DesktopCoordinates.Bottom);

                if (bounds.Contains(targetRect.Left, targetRect.Top) &&
                    bounds.Contains(targetRect.Right - 1, targetRect.Bottom - 1))
                {
                    adapter = currentAdapter;
                    output = currentOutput1;
                    outputBounds = bounds;
                    return true;
                }

                currentOutput1.Dispose();
            }

            currentAdapter.Dispose();
        }

        return false;
    }
}
