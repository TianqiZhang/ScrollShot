 Now I have everything I need. Let me design the complete implementation plan.

       ---
       Here is the detailed implementation plan for ScrollShot.

       .NET Version Recommendation

       Stick with .NET 8 as specified. .NET 10 (currently installed) offers no meaningful advantages for this project --
        WPF APIs, DXGI interop, and the NuGet packages used are all stable on .NET 8. .NET 8 has LTS support until
       November 2026, while .NET 10 (released May 2025) is also LTS but has a smaller deployment base. The only .NET 10
       feature of minor relevance would be potential runtime performance improvements, but nothing that changes the
       architecture. Broader compatibility wins here.

       ---
       Dependency Graph and Build Phases

       The project decomposes into 6 phases with clear dependency edges. Here is the dependency graph:

       Phase 0: Solution scaffold, shared types, interfaces
           |
           +---> Phase 1a: Capture layer (DXGI + GDI)     [depends on: interfaces]
           |         |
           +---> Phase 1b: Scroll algorithms (zone/overlap) [depends on: interfaces, pure logic]
           |         |
           |     Phase 1c: Editor data model (undo/redo)    [depends on: interfaces, pure logic]
           |
       Phase 2: Overlay UI (selection, live preview)   [depends on: Phase 0 interfaces, Phase 1a capture]
           |
       Phase 3: Scroll engine orchestrator             [depends on: Phase 1a, 1b, Phase 2 for preview events]
           |
       Phase 4: Preview Editor UI                      [depends on: Phase 1c, Phase 3 for data]
           |
       Phase 5: App Shell integration                  [depends on: everything]

       Phases 1a, 1b, 1c are fully independent and can be built in parallel.

       ---
       Phase 0 -- Solution Scaffold and Shared Contracts

       Goal: Create the solution structure, all projects, shared types, and the interfaces that define contracts between
        components. This is the foundation everything else depends on.

       Task 0.1: Solution and Project Structure

       Files to create:
       - ScrollShot/ScrollShot.sln
       - ScrollShot/src/ScrollShot.App/ScrollShot.App.csproj (WPF, net8.0-windows, OutputType: WinExe)
       - ScrollShot/src/ScrollShot.Capture/ScrollShot.Capture.csproj (classlib, net8.0-windows)
       - ScrollShot/src/ScrollShot.Scroll/ScrollShot.Scroll.csproj (classlib, net8.0-windows)
       - ScrollShot/src/ScrollShot.Overlay/ScrollShot.Overlay.csproj (classlib with WPF, net8.0-windows)
       - ScrollShot/src/ScrollShot.Editor/ScrollShot.Editor.csproj (classlib with WPF, net8.0-windows)
       - ScrollShot/tests/ScrollShot.Capture.Tests/ScrollShot.Capture.Tests.csproj (xUnit)
       - ScrollShot/tests/ScrollShot.Scroll.Tests/ScrollShot.Scroll.Tests.csproj (xUnit)
       - ScrollShot/tests/ScrollShot.Editor.Tests/ScrollShot.Editor.Tests.csproj (xUnit)
       - ScrollShot/Directory.Build.props (shared settings: nullable enable, implicit usings, LangVersion latest)
       - ScrollShot/.editorconfig

       NuGet references by project:
       - ScrollShot.Capture: Vortice.DXGI, Vortice.Direct3D11, System.Drawing.Common
       - ScrollShot.Scroll: System.Drawing.Common (for bitmap manipulation of segments)
       - ScrollShot.Overlay: references ScrollShot.Capture, ScrollShot.Scroll
       - ScrollShot.Editor: references ScrollShot.Scroll (for the capture result model)
       - ScrollShot.App: references all src projects; NuGet Hardcodet.NotifyIcon.Wpf (tray icon)
       - All test projects: xunit, xunit.runner.visualstudio, Moq, FluentAssertions

       Project references (dependency graph):
       ScrollShot.App --> ScrollShot.Overlay, ScrollShot.Editor, ScrollShot.Capture, ScrollShot.Scroll
       ScrollShot.Overlay --> ScrollShot.Capture, ScrollShot.Scroll
       ScrollShot.Editor --> ScrollShot.Scroll
       ScrollShot.Scroll --> ScrollShot.Capture (for IScreenCapturer interface only)

       Testing: dotnet build succeeds. dotnet test runs (no tests yet, zero failures).

       Task 0.2: Shared Types and Value Objects

       File: ScrollShot/src/ScrollShot.Capture/Models/ScreenRect.cs

       public readonly record struct ScreenRect(int X, int Y, int Width, int Height);

       File: ScrollShot/src/ScrollShot.Capture/Models/CapturedFrame.cs

       A value type wrapping a bitmap (or raw pixel buffer) plus metadata: timestamp, DPI scale factor, source
       rectangle.

       File: ScrollShot/src/ScrollShot.Scroll/Models/ScrollDirection.cs

       public enum ScrollDirection { Vertical, Horizontal }

       File: ScrollShot/src/ScrollShot.Scroll/Models/ZoneLayout.cs

       Record holding FixedTop, FixedBottom, FixedLeft, FixedRight (pixel counts), and ScrollBand (a ScreenRect relative
        to the capture area).

       File: ScrollShot/src/ScrollShot.Scroll/Models/ScrollSegment.cs

       A segment of new scrolling content: the bitmap data (or a reference to a temp file) plus its pixel offset in the
       final stitched image.

       File: ScrollShot/src/ScrollShot.Scroll/Models/CaptureResult.cs

       The complete output of a scroll capture session: the list of ScrollSegments, the ZoneLayout, the fixed-region
       bitmaps, the ScrollDirection, and total composed dimensions.

       File: ScrollShot/src/ScrollShot.Editor/Models/EditState.cs

       The editor's state model: trim range, list of cut ranges, crop rect, chrome-included flag. Designed for undo/redo
        (immutable snapshots or command pattern).

       Testing: Unit tests for record equality, serialization if needed.

       Task 0.3: Core Interfaces

       These interfaces are the contracts between components. They must be defined before any implementation begins. All
        interfaces go in their respective project.

       File: ScrollShot/src/ScrollShot.Capture/IScreenCapturer.cs

       public interface IScreenCapturer : IDisposable
       {
           /// Initialize capture for the given screen region.
           void Initialize(ScreenRect region);

           /// Capture a single frame. Returns null if no new frame available.
           CapturedFrame? CaptureFrame();

           /// Whether this capturer is available on the current system.
           bool IsAvailable { get; }
       }

       File: ScrollShot/src/ScrollShot.Scroll/IZoneDetector.cs

       public interface IZoneDetector
       {
           /// Compare two frames and detect the zone layout.
           ZoneLayout DetectZones(CapturedFrame previous, CapturedFrame current, ScrollDirection direction);

           /// Refine existing zones given a new frame pair (re-verification).
           ZoneLayout RefineZones(ZoneLayout existing, CapturedFrame previous, CapturedFrame current, ScrollDirection
       direction);
       }

       File: ScrollShot/src/ScrollShot.Scroll/IOverlapMatcher.cs

       public interface IOverlapMatcher
       {
           /// Find the overlap between two scrolling-band bitmaps.
           /// Returns the number of overlapping rows/columns.
           /// Returns 0 if no overlap found (concatenate).
           /// Returns -1 if frames are identical (skip).
           OverlapResult FindOverlap(ReadOnlySpan<byte> previousBand, ReadOnlySpan<byte> currentBand,
                                      int width, int height, ScrollDirection direction);
       }

       File: ScrollShot/src/ScrollShot.Scroll/IScrollSession.cs

       public interface IScrollSession : IDisposable
       {
           event Action<ScrollSegment>? SegmentAdded;
           event Action<ZoneLayout>? ZonesDetected;
           event Action<Bitmap>? PreviewUpdated;

           void Start(ScreenRect region, ScrollDirection direction);
           void ProcessFrame(CapturedFrame frame);
           void Finish();
           CaptureResult GetResult();
       }

       File: ScrollShot/src/ScrollShot.Editor/IImageCompositor.cs

       public interface IImageCompositor
       {
           /// Compose the final image from a CaptureResult and EditState.
           Bitmap Compose(CaptureResult result, EditState editState);
       }

       Depends on: Nothing (this is the root).
       Produces: All shared types and interfaces that everything else depends on.
       Testing: Compiles. Types have unit tests for construction and equality.

       ---
       Phase 1a -- Capture Layer

       Task 1a.1: GDI Screen Capturer (Fallback)

       File: ScrollShot/src/ScrollShot.Capture/GdiScreenCapturer.cs

       Implements IScreenCapturer using System.Drawing, P/Invoke for BitBlt. This is the simpler implementation and
       serves as the fallback.

       Key implementation details:
       - P/Invoke: GetDC, CreateCompatibleDC, CreateCompatibleBitmap, BitBlt, DeleteObject, ReleaseDC
       - IsAvailable always returns true (GDI is always available on Windows)
       - Respects physical pixels (DPI-unaware capture context)
       - Create a helper class NativeMethods.cs for all P/Invoke declarations

       Files:
       - ScrollShot/src/ScrollShot.Capture/GdiScreenCapturer.cs
       - ScrollShot/src/ScrollShot.Capture/Interop/NativeMethods.cs

       Testing: Manual only (requires a running desktop). Create an integration test that can be skipped in CI: capture
       a known region, verify the bitmap dimensions match, verify it's not all-black.

       Task 1a.2: DXGI Desktop Duplication Capturer

       File: ScrollShot/src/ScrollShot.Capture/DxgiScreenCapturer.cs

       Implements IScreenCapturer using Vortice.DXGI and Vortice.Direct3D11.

       Key implementation details:
       - Create IDXGIFactory1, enumerate adapters and outputs
       - Call IDXGIOutput1.DuplicateOutput to get IDXGIOutputDuplication
       - CaptureFrame(): call AcquireNextFrame, map the ID3D11Texture2D, copy to a staging texture, map to CPU memory,
       crop to the requested ScreenRect, return as CapturedFrame
       - IsAvailable: try to create the duplication; return false if it fails (e.g., on RDP, or older GPUs)
       - Handle DXGI_ERROR_WAIT_TIMEOUT (no new frame) by returning null
       - Handle DXGI_ERROR_ACCESS_LOST by re-creating the duplication
       - Multi-monitor: determine which output(s) intersect the requested ScreenRect; for cross-monitor capture, capture
        from multiple outputs and compose

       Files:
       - ScrollShot/src/ScrollShot.Capture/DxgiScreenCapturer.cs
       - ScrollShot/src/ScrollShot.Capture/DxgiHelper.cs (adapter/output enumeration, staging texture creation)

       Depends on: Task 0 interfaces and types.
       Testing: Manual only. Integration test (skippable): capture a region, verify non-null bitmap, correct dimensions.

       Task 1a.3: Capture Factory

       File: ScrollShot/src/ScrollShot.Capture/ScreenCapturerFactory.cs

       public static class ScreenCapturerFactory
       {
           public static IScreenCapturer Create(ScreenRect region)
           {
               var dxgi = new DxgiScreenCapturer();
               if (dxgi.IsAvailable) { dxgi.Initialize(region); return dxgi; }
               var gdi = new GdiScreenCapturer();
               gdi.Initialize(region);
               return gdi;
           }
       }

       Testing: Unit test with a mock that checks the fallback logic (if DXGI returns IsAvailable = false, GDI is used).

       ---
       Phase 1b -- Scroll Algorithms (Pure Logic, Highly Testable)

       This is the algorithmic core. All classes here operate on raw pixel data (byte arrays/spans), not on bitmaps
       directly. This makes them testable with synthetic pixel data.

       Task 1b.1: Row/Column Hash Computation

       File: ScrollShot/src/ScrollShot.Scroll/Algorithms/RowColumnHash.cs

       Static utility methods:
       - long[] ComputeRowHashes(ReadOnlySpan<byte> pixels, int width, int height, int stride) -- sum of pixel values
       per row
       - long[] ComputeColumnHashes(ReadOnlySpan<byte> pixels, int width, int height, int stride) -- sum per column
       - double RowDifference(ReadOnlySpan<byte> rowA, ReadOnlySpan<byte> rowB) -- normalized pixel difference for a
       single row

       Testing: Thorough unit tests with synthetic pixel arrays. Test cases:
       - Identical rows return difference 0
       - Completely different rows return difference 1.0
       - Known pixel patterns produce expected hashes
       - Edge cases: single-pixel-wide images, single-pixel-tall images

       File: ScrollShot/tests/ScrollShot.Scroll.Tests/Algorithms/RowColumnHashTests.cs

       Task 1b.2: Zone Detector

       File: ScrollShot/src/ScrollShot.Scroll/Algorithms/ZoneDetector.cs

       Implements IZoneDetector.

       Algorithm (vertical scroll case):
       1. For each row, compute pixel difference between Frame N-1 and Frame N
       2. Scan from top: while difference < threshold, mark as fixedTop
       3. Scan from bottom: while difference < threshold, mark as fixedBottom
       4. Scan from left within the remaining band: while all pixels in that column are unchanged, mark as fixedLeft
       5. Scan from right: same for fixedRight
       6. The remaining rectangle is the scrollBand

       Horizontal scroll case mirrors the logic with rows/columns swapped.

       RefineZones: compare new zone detection against existing. If they differ by more than N pixels, update (handles
       dynamic chrome like appearing toolbars). Otherwise keep the established zones for stability.

       Testing: This is the most testable part of the project. Test with synthetic frame pairs:
       - Two frames with identical top 50px and bottom 20px, changed middle: detect fixedTop=50, fixedBottom=20
       - Two frames with no fixed regions: all content is scrolling band
       - Two frames with fixed left/right margins
       - Frame pair where everything is identical (no scroll happened)
       - Frame pair where everything changed (no fixed regions)
       - Zone refinement: verify it keeps stable zones when new detection differs by only 1-2 pixels
       - Threshold sensitivity tests

       File: ScrollShot/tests/ScrollShot.Scroll.Tests/Algorithms/ZoneDetectorTests.cs

       Task 1b.3: Overlap Matcher

       File: ScrollShot/src/ScrollShot.Scroll/Algorithms/OverlapMatcher.cs

       Implements IOverlapMatcher.

       Algorithm (vertical scroll case):
       1. Compute row hashes for both the previous and current scrolling band
       2. Use a sliding window: try each offset from 1 to height-1
       3. For each offset, compare row hashes first (cheap). If hashes match, compute per-pixel SAD for that overlap
       region
       4. Return the offset with the lowest SAD below a threshold
       5. If all offsets above threshold: return OverlapResult.NoMatch (concatenate)
       6. If the full frame is identical: return OverlapResult.Identical (skip)

       Optimization: start the search from likely offsets (small scrolls are more common) and early-exit when a good
       match is found.

       File: ScrollShot/src/ScrollShot.Scroll/Models/OverlapResult.cs

       public readonly record struct OverlapResult(int OverlapRows, bool IsIdentical, double Confidence);

       Testing: Heavily tested with synthetic data:
       - Two bands where the bottom half of previous == top half of current: detect overlap at 50%
       - Two completely different bands: no overlap found
       - Two identical bands: detect as identical
       - Bands with 1-pixel scroll: detect overlap at height-1
       - Bands with sub-pixel differences (antialiasing): still match within threshold
       - Performance test: verify it handles a 1920px-wide band in reasonable time

       File: ScrollShot/tests/ScrollShot.Scroll.Tests/Algorithms/OverlapMatcherTests.cs

       Task 1b.4: Image Compositor

       File: ScrollShot/src/ScrollShot.Scroll/Composition/ImageCompositor.cs

       Implements IImageCompositor.

       Takes a CaptureResult (zone layout, fixed-region bitmaps, list of scroll segments) and an EditState (trim, cuts,
       chrome toggle, crop) and produces the final PNG bitmap.

       Algorithm:
       1. Calculate total height/width of final image based on segments minus trimmed/cut regions
       2. If chrome included: allocate bitmap = fixedTop + total_scroll_height + fixedBottom (vertical case)
       3. Draw fixedTop, then each scroll segment in order (skipping cut ranges), then fixedBottom
       4. Apply crop rectangle if set
       5. Return the composed Bitmap

       Testing: Unit tests with small synthetic segments:
       - Compose 3 segments of 10px each: result is 30px tall
       - Compose with trim (remove first 5px and last 5px): result is 20px tall
       - Compose with a cut (remove pixels 10-15): result is 25px tall
       - Chrome toggle off: only scroll segments, no fixed regions
       - Chrome toggle on: includes fixed top/bottom

       File: ScrollShot/tests/ScrollShot.Scroll.Tests/Composition/ImageCompositorTests.cs

       Task 1b.5: Pixel Buffer Utilities

       File: ScrollShot/src/ScrollShot.Scroll/Algorithms/PixelBuffer.cs

       Helper class for working with raw pixel data:
       - Extract a sub-rectangle from a pixel buffer
       - Compare two pixel buffers (SAD)
       - Convert between Bitmap and byte[] (lock/unlock bits)
       - Downscale a pixel buffer for the preview thumbnail

       Testing: Unit tests with small known pixel arrays.

       File: ScrollShot/tests/ScrollShot.Scroll.Tests/Algorithms/PixelBufferTests.cs

       ---
       Phase 1c -- Editor Data Model (Pure Logic, Testable)

       Task 1c.1: Edit State and Command Pattern

       Files:
       - ScrollShot/src/ScrollShot.Editor/Models/EditState.cs
       - ScrollShot/src/ScrollShot.Editor/Models/CutRange.cs -- readonly record struct CutRange(int StartPixel, int
       EndPixel)
       - ScrollShot/src/ScrollShot.Editor/Models/TrimRange.cs -- readonly record struct TrimRange(int HeadTrimPixels,
       int TailTrimPixels)
       - ScrollShot/src/ScrollShot.Editor/Models/CropRect.cs
       - ScrollShot/src/ScrollShot.Editor/Commands/IEditCommand.cs
       - ScrollShot/src/ScrollShot.Editor/Commands/TrimCommand.cs
       - ScrollShot/src/ScrollShot.Editor/Commands/CutCommand.cs
       - ScrollShot/src/ScrollShot.Editor/Commands/CropCommand.cs
       - ScrollShot/src/ScrollShot.Editor/Commands/ChromeToggleCommand.cs
       - ScrollShot/src/ScrollShot.Editor/Commands/EditCommandStack.cs (undo/redo stack)

       IEditCommand:
       public interface IEditCommand
       {
           EditState Apply(EditState state);
           EditState Undo(EditState state);
           string Description { get; }
       }

       EditCommandStack manages a list of applied commands and a redo stack. Undo() pops last command, calls Undo,
       pushes to redo. Redo() does the reverse.

       Testing: Extensively unit tested:
       - Apply a trim, verify EditState changes
       - Undo the trim, verify EditState reverts
       - Apply trim + cut + crop, undo all three, redo one
       - Edge cases: undo when stack is empty, redo when redo stack is empty
       - Multiple cuts: verify they don't overlap

       File: ScrollShot/tests/ScrollShot.Editor.Tests/Commands/EditCommandStackTests.cs

       ---
       Phase 2 -- Selection Overlay UI

       Task 2.1: Multi-Monitor Virtual Screen Helper

       File: ScrollShot/src/ScrollShot.Overlay/Helpers/ScreenHelper.cs

       Gets the virtual screen bounds spanning all monitors (using System.Windows.Forms.Screen.AllScreens or WPF's
       SystemParameters). Computes the bounding rectangle in physical pixels. Handles per-monitor DPI scaling.

       Testing: Limited unit testing (mock Screen.AllScreens). Manual testing needed for actual multi-monitor setups.

       Task 2.2: Selection Overlay Window

       Files:
       - ScrollShot/src/ScrollShot.Overlay/SelectionOverlayWindow.xaml
       - ScrollShot/src/ScrollShot.Overlay/SelectionOverlayWindow.xaml.cs

       A WPF Window with:
       - WindowStyle="None", AllowsTransparency="True", Topmost="True"
       - Covers the entire virtual screen
       - Dark semi-transparent background (#80000000)
       - On mouse down: start selection
       - On mouse move: draw selection rectangle (clear/bright area, rest dimmed)
       - On mouse up: selection complete, wait for Enter/Scroll/Esc
       - On Enter: raise InstantCaptureRequested(ScreenRect) event
       - On mouse wheel: raise ScrollCaptureStarted(ScreenRect, ScrollDirection) event
       - On Esc: raise Cancelled event

       Window styles for self-exclusion:
       - Set extended style WS_EX_TOOLWINDOW to hide from taskbar/Alt-Tab
       - Set WS_EX_TRANSPARENT on the non-interactive areas (but the selection handles and Done button must still
       receive input -- this requires careful layering or using a separate window for interactive elements)

       DPI handling: Convert mouse coordinates from WPF device-independent pixels to physical pixels for the ScreenRect.

       Depends on: Task 0 types, Task 2.1 screen helper.
       Testing: Manual only. This is pure UI.

       Task 2.3: Live Preview Strip Control

       Files:
       - ScrollShot/src/ScrollShot.Overlay/Controls/LivePreviewStrip.xaml
       - ScrollShot/src/ScrollShot.Overlay/Controls/LivePreviewStrip.xaml.cs

       A WPF UserControl (or a separate small window) that:
       - Appears alongside the selection rectangle during scroll capture
       - Displays a downscaled thumbnail of the growing stitched image
       - Updates when IScrollSession.PreviewUpdated fires
       - Shows scroll direction indicator
       - Contains the "Done" button
       - Uses SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE) or is on a separate excluded window to avoid capturing
       itself

       Depends on: Task 0 interfaces (IScrollSession events).
       Testing: Manual only.

       ---
       Phase 3 -- Scroll Engine Orchestrator

       Task 3.1: ScrollSession Implementation

       File: ScrollShot/src/ScrollShot.Scroll/ScrollSession.cs

       Implements IScrollSession. This is the orchestrator that ties the algorithms together.

       State machine:
       1. Idle -- waiting for Start()
       2. Initializing -- first two frames: detect zones
       3. Capturing -- processing frames: overlap match, stitch, emit segments
       4. Finished -- Finish() called, result available

       ProcessFrame(CapturedFrame frame):
       1. If first frame: store as reference frame, no output
       2. If second frame: run zone detection, store zone layout, compute overlap (should be minimal), store first delta
        segment, fire ZonesDetected
       3. Subsequent frames:
         - Every 10th frame: re-verify zones
         - Extract scrolling band using current zone layout
         - Run overlap matcher against previous band
         - If identical: skip
         - If overlap found: extract the non-overlapping delta, add as segment
         - If no overlap: add entire band as segment (never miss content)
         - Generate downscaled preview, fire PreviewUpdated and SegmentAdded

       Memory management:
       - Keep a configurable max segments in memory (e.g., 100)
       - When exceeded, flush oldest segments to temp files
       - CaptureResult.GetResult() resolves temp files back into memory at save time

       Depends on: Task 1a (IScreenCapturer), Task 1b.1-1b.5 (all algorithms).
       Testing: Unit tests using mock IScreenCapturer with predetermined frame sequences:
       - Feed a sequence of frames with known overlaps, verify correct segments emitted
       - Feed identical frames, verify they're skipped
       - Feed frames with no overlap, verify concatenation
       - Verify zone detection fires after the second frame
       - Verify zone re-verification happens every 10 frames
       - Performance/stress test with 100+ synthetic frames

       File: ScrollShot/tests/ScrollShot.Scroll.Tests/ScrollSessionTests.cs

       Task 3.2: Capture Thread Controller

       File: ScrollShot/src/ScrollShot.Scroll/CaptureController.cs

       Manages the background capture thread:
       - Owns the IScreenCapturer instance
       - On scroll event: trigger capture on background thread
       - Thread-safe queue of captured frames passed to ScrollSession.ProcessFrame()
       - Handles DXGI errors (access lost, timeout) with retry logic

       Depends on: Task 1a (capture), Task 3.1 (scroll session).
       Testing: Unit tests with mock capturer: verify thread safety, verify error handling and retry.

       ---
       Phase 4 -- Preview Editor UI

       Task 4.1: Timeline Strip Control

       Files:
       - ScrollShot/src/ScrollShot.Editor/Controls/TimelineStrip.xaml
       - ScrollShot/src/ScrollShot.Editor/Controls/TimelineStrip.xaml.cs

       Custom WPF control that:
       - Displays a miniaturized version of the full stitched image
       - Can be oriented vertically or horizontally based on scroll direction
       - Trim handles (orange) at head and tail, draggable
       - Cut zones rendered as red semitransparent bands
       - Click on timeline scrolls the main viewport to that position
       - Click-drag selects a range for cutting
       - Chrome regions visually distinguished (different tint)

       Depends on: Task 1c (EditState, CutRange, TrimRange), Task 1b.4 (compositor for rendering preview).
       Testing: Manual only for UI. The underlying data transformations (pixel position to scroll position mapping) are
       testable.

       Task 4.2: Viewport Control (Zoom/Pan)

       Files:
       - ScrollShot/src/ScrollShot.Editor/Controls/ImageViewport.xaml
       - ScrollShot/src/ScrollShot.Editor/Controls/ImageViewport.xaml.cs

       Custom WPF control wrapping a ScrollViewer and Image:
       - Scroll wheel zooms (centered on cursor)
       - Click-drag pans
       - Fit-to-view and 1:1 zoom buttons
       - Crop overlay rectangle (draggable corners/edges) when crop tool is active

       Testing: Manual only.

       Task 4.3: Editor Window and ViewModel

       Files:
       - ScrollShot/src/ScrollShot.Editor/PreviewEditorWindow.xaml
       - ScrollShot/src/ScrollShot.Editor/PreviewEditorWindow.xaml.cs
       - ScrollShot/src/ScrollShot.Editor/ViewModels/PreviewEditorViewModel.cs

       MVVM pattern. The ViewModel:
       - Holds the CaptureResult and the EditCommandStack
       - Exposes commands: TrimCommand, CutCommand, CropCommand, ChromeToggleCommand, UndoCommand, RedoCommand,
       SaveCommand, CopyCommand, DiscardCommand
       - SaveCommand: calls IImageCompositor.Compose(), saves to PNG with auto-name
       - CopyCommand: composes and places on clipboard
       - DiscardCommand: prompts if unsaved edits, then closes
       - Calculates the layout (vertical vs horizontal) based on ScrollDirection

       Depends on: Task 1c (edit model), Task 1b.4 (compositor), Task 4.1-4.2 (controls).
       Testing: ViewModel is testable with mocked compositor. Test save file naming, copy-to-clipboard behavior (via
       mock), discard confirmation logic.

       File: ScrollShot/tests/ScrollShot.Editor.Tests/ViewModels/PreviewEditorViewModelTests.cs

       ---
       Phase 5 -- App Shell Integration

       Task 5.1: System Tray and Control Window

       Files:
       - ScrollShot/src/ScrollShot.App/App.xaml (no StartupUri, starts to tray)
       - ScrollShot/src/ScrollShot.App/App.xaml.cs
       - ScrollShot/src/ScrollShot.App/MainWindow.xaml (small control window)
       - ScrollShot/src/ScrollShot.App/MainWindow.xaml.cs
       - ScrollShot/src/ScrollShot.App/TrayIconManager.cs

       App.xaml.cs:
       - Override OnStartup: create tray icon, register hotkey, show control window
       - Tray icon context menu: "New Capture", "Settings", "Exit"
       - Control window: "New Capture" button, settings link, minimize-to-tray on close

       Uses Hardcodet.NotifyIcon.Wpf for the tray icon.

       Depends on: Task 2 (overlay), Task 4 (editor), all else.
       Testing: Manual only.

       Task 5.2: Global Hotkey Registration

       File: ScrollShot/src/ScrollShot.App/Services/GlobalHotkeyService.cs

       P/Invoke RegisterHotKey / UnregisterHotKey. Listens via HwndSource for WM_HOTKEY.

       Configurable key combination (default Ctrl+Shift+S). Handles conflicts gracefully (notify user if hotkey is
       taken).

       File: ScrollShot/src/ScrollShot.App/Interop/HotkeyNativeMethods.cs

       Testing: Manual only (requires desktop interaction).

       Task 5.3: Settings Management

       Files:
       - ScrollShot/src/ScrollShot.App/Services/SettingsService.cs
       - ScrollShot/src/ScrollShot.App/Models/AppSettings.cs

       JSON-based settings stored in %APPDATA%/ScrollShot/settings.json. Properties:
       - HotkeyModifiers (Ctrl, Alt, Shift flags), HotkeyKey
       - SaveFolder (default: Pictures/Screenshots/)
       - StartWithWindows (bool)

       Uses System.Text.Json for serialization (built into .NET 8, no extra dependency).

       Testing: Unit testable: serialize/deserialize settings, default values, file-not-found returns defaults.

       File: ScrollShot/tests/ScrollShot.Capture.Tests/SettingsServiceTests.cs (or a new ScrollShot.App.Tests project)

       Task 5.4: DPI Awareness Manifest

       File: ScrollShot/src/ScrollShot.App/app.manifest

       Set per-monitor DPI awareness v2:
       <application xmlns="urn:schemas-microsoft-com:asm.v3">
         <windowsSettings>
           <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
         </windowsSettings>
       </application>

       Task 5.5: Orchestration Flow

       File: ScrollShot/src/ScrollShot.App/Services/CaptureOrchestrator.cs

       The main workflow controller:
       1. On hotkey/button press: create and show SelectionOverlayWindow
       2. On InstantCaptureRequested: capture one frame via IScreenCapturer, create CaptureResult with single segment,
       open PreviewEditorWindow
       3. On ScrollCaptureStarted: create CaptureController + ScrollSession, start capture loop. When overlay fires
       Done: call ScrollSession.Finish(), open PreviewEditorWindow with result
       4. On Cancelled: dismiss overlay, clean up

       Depends on: Everything.
       Testing: Integration test with mocks for all dependencies. Verify the state machine transitions correctly.

       ---
       MVP Path (Minimum Viable Product)

       To get something working end-to-end as fast as possible, this is the minimal ordering:

       1. Phase 0 -- scaffold + interfaces (1 day)
       2. Task 1a.1 -- GDI capturer only (skip DXGI for MVP) (half day)
       3. Task 1b.2 -- Zone detector (1 day)
       4. Task 1b.3 -- Overlap matcher (1 day)
       5. Task 1b.4 -- Image compositor (simple version) (half day)
       6. Task 3.1 -- Scroll session (1 day)
       7. Task 2.2 -- Selection overlay (minimal, single monitor) (1 day)
       8. Task 5.5 -- Orchestrator (minimal) (half day)
       9. Task 4.3 -- Editor window (save-only, no timeline, no editing) (half day)

       This gives a working app that can: hotkey -> select area -> scroll -> save stitched PNG. No editing, no live
       preview, no DXGI, single monitor only. Then layer on:

       - DXGI capturer (Task 1a.2)
       - Live preview strip (Task 2.3)
       - Timeline strip + trim/cut editing (Tasks 1c.1, 4.1, 4.2)
       - Multi-monitor support (Task 2.1)
       - Settings persistence (Task 5.3)

       ---
       Summary: Full Task List with Dependencies







       ┌──────┬───────────────────────────┬──────────────────────┬─────────────┬────────────────┐
       │ Task │           Name            │      Depends On      │    Type     │  Testability   │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 0.1  │ Solution scaffold         │ --                   │ Scaffold    │ Build succeeds │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 0.2  │ Shared types              │ 0.1                  │ Model       │ Unit tests     │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 0.3  │ Core interfaces           │ 0.1                  │ Interface   │ Compiles       │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 1a.1 │ GDI capturer              │ 0.2, 0.3             │ Platform    │ Manual + integ │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 1a.2 │ DXGI capturer             │ 0.2, 0.3             │ Platform    │ Manual + integ │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 1a.3 │ Capture factory           │ 1a.1, 1a.2           │ Logic       │ Unit tests     │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 1b.1 │ Row/column hashing        │ 0.2                  │ Algorithm   │ Unit tests     │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 1b.2 │ Zone detector             │ 0.2, 0.3, 1b.1       │ Algorithm   │ Unit tests     │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 1b.3 │ Overlap matcher           │ 0.2, 0.3, 1b.1       │ Algorithm   │ Unit tests     │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 1b.4 │ Image compositor          │ 0.2, 1c.1            │ Algorithm   │ Unit tests     │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 1b.5 │ Pixel buffer utilities    │ 0.2                  │ Utility     │ Unit tests     │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 1c.1 │ Edit state + commands     │ 0.2                  │ Model       │ Unit tests     │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 2.1  │ Screen helper             │ 0.2                  │ Utility     │ Limited unit   │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 2.2  │ Selection overlay         │ 0.2, 2.1             │ UI          │ Manual         │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 2.3  │ Live preview strip        │ 0.3                  │ UI          │ Manual         │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 3.1  │ Scroll session            │ 1b.2, 1b.3, 1b.5     │ Logic       │ Unit tests     │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 3.2  │ Capture thread controller │ 1a.3, 3.1            │ Infra       │ Unit tests     │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 4.1  │ Timeline strip control    │ 1c.1                 │ UI          │ Manual         │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 4.2  │ Viewport control          │ --                   │ UI          │ Manual         │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 4.3  │ Editor window + ViewModel │ 1c.1, 1b.4, 4.1, 4.2 │ UI+Logic    │ VM unit tests  │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 5.1  │ Tray + control window     │ 2.2, 4.3             │ UI          │ Manual         │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 5.2  │ Global hotkey             │ 0.1                  │ Platform    │ Manual         │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 5.3  │ Settings                  │ 0.1                  │ Logic       │ Unit tests     │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 5.4  │ DPI manifest              │ 0.1                  │ Config      │ Manual         │
       ├──────┼───────────────────────────┼──────────────────────┼─────────────┼────────────────┤
       │ 5.5  │ Capture orchestrator      │ all of the above     │ Integration │ Integration    │
       └──────┴───────────────────────────┴──────────────────────┴─────────────┴────────────────┘

       ---
       Critical Files for Implementation

       - F:/bead/screenshot/docs/superpowers/specs/2026-04-07-scrollshot-design.md -- the authoritative spec that all
       implementation must follow
       - ScrollShot/src/ScrollShot.Scroll/Algorithms/ZoneDetector.cs -- the most algorithmically complex and
       critical-to-test component; incorrect zone detection breaks the entire stitching pipeline
       - ScrollShot/src/ScrollShot.Scroll/Algorithms/OverlapMatcher.cs -- second most critical algorithm; determines
       stitching quality and whether content is missed or duplicated
       - ScrollShot/src/ScrollShot.Scroll/ScrollSession.cs -- the orchestrator that ties capture, zone detection,
       overlap matching, and segment storage into a working scroll capture session
       - ScrollShot/src/ScrollShot.Capture/DxgiScreenCapturer.cs -- the primary capture mechanism; the most
       platform-dependent and hardest-to-debug component, with complex Vortice/COM interop