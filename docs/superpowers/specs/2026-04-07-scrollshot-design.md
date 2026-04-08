# ScrollShot — Windows Scrolling Screenshot App

**Date:** 2026-04-07  
**Status:** Design approved, pending implementation

## Overview

ScrollShot is a native Windows screenshot app that supports both instant and scrolling capture. After selecting a screen region, the user can either take an instant screenshot (Enter) or begin scrolling to create a long stitched screenshot. A live preview strip shows progress during capture, and a timeline-style editor allows trimming and cutting the result before saving.

**Target user:** General consumer — the app should feel as simple as Windows Snipping Tool, with scrolling superpowers.

**Technology:** C# / WPF / .NET 8, using DXGI Desktop Duplication for screen capture.

## User Interaction Flow

### Activation

Two entry points:

1. **Global hotkey** (default `Ctrl+Shift+S`, configurable) — for repeat users
2. **System tray icon + small control window** with a "New Capture" button — for discoverability

The app runs as a system tray application. The control window is optional and can be closed; the tray icon remains.

### Area Selection

On activation:

1. A transparent, topmost overlay window covers all monitors
2. Screen dims with a semi-transparent dark overlay
3. Cursor changes to a crosshair
4. User click-drags to define a rectangular selection area
5. The selected area is highlighted (clear), the rest stays dimmed
6. **Esc** at any point cancels and dismisses the overlay

### Branch Point: Instant vs Scroll

After area selection, the user decides implicitly:

- **Enter** → instant screenshot of the selected area, jumps to Preview Editor
- **Scroll** (mouse wheel or trackpad) → enters scroll-capture mode
- **Esc** → cancel

There is no explicit mode switch. The scroll action itself is the trigger — like iPhone panorama, the gesture begins the capture.

### Scroll Capture Mode

Once scrolling begins:

1. The overlay remains dimmed except for the selected area
2. The user scrolls naturally (the content beneath scrolls as it would normally)
3. A **live preview strip** appears alongside the selection:
   - **Vertical scroll** → preview strip on the **right** side, growing downward
   - **Horizontal scroll** → preview strip **below**, growing rightward
4. A direction indicator (↓ or →) confirms the scroll direction
5. A small floating **"Done ✓"** button appears near the preview strip

Scroll direction is **locked** after the first scroll input:
- Mouse wheel delta Y → vertical
- Shift+wheel or horizontal trackpad gesture → horizontal

To finish: **Enter** or click **"Done ✓"** → proceeds to Preview Editor. **Esc** → discard and cancel.

### Preview Editor

A lightweight window opens showing the captured result with editing capabilities (see Preview Editor section below).

### Output

From the Preview Editor:

- **Save** → composites final PNG, saves to configured folder with auto-name `ScrollShot_YYYYMMDD_HHmmss.png`
- **Copy** → composites and places on clipboard, toast confirms
- **Discard** → throws away capture (confirmation prompt if edits were made)

## Scroll Capture: Technical Design

### The Core Challenge: Fixed vs Scrolling Regions

The user's selected area is typically larger than the scrollable content. For example, selecting a browser window includes the toolbar, tabs, and status bar — all of which remain fixed while only the content area scrolls. The algorithm must detect and handle this.

### Frame Capture Pipeline

#### 1. CAPTURE — DXGI Desktop Duplication

- Uses `IDXGIOutputDuplication` for GPU-accelerated screen capture
- Captures only the selected rectangle, not the full screen
- **Cursor excluded** from capture (DXGI option to omit cursor)
- Runs on a dedicated background thread
- Triggered on each scroll event (not a fixed timer) to capture every intermediate state
- Falls back to BitBlt (GDI) if DXGI is unavailable (older systems, RDP sessions)

#### 2. ZONE SPLIT — Detect Fixed vs Scrolling Regions

Compares Frame N with Frame N-1:

- **For vertical scroll:** compare each row top-to-bottom. Rows with pixel difference below a threshold are "fixed."
- **For horizontal scroll:** compare each column left-to-right.
- Scans inward from **all four edges** (top, bottom, left, right) to find the scrolling rectangle
- Output: `fixedTop`, `fixedBottom`, `fixedLeft`, `fixedRight` margins, and the inner `scrollBand` rectangle
- Established on the first two captured frames
- Re-verified every ~10 frames to handle dynamic chrome (e.g. a toolbar that appears/disappears)

#### 3. OVERLAP — Match Within the Scrolling Band

- Extracts the scrolling band from both the previous and current frame
- Computes row/column hash signatures (pixel-sum per row/column) within the band
- Slides the new frame's band against the previous to find the best overlap position (minimize Sum of Absolute Differences)
- **No match found?** Concatenate directly — **never miss content** is the priority
- **100% match (identical frame)?** Skip — content hasn't moved yet

#### 4. STITCH — Incremental Strip Building

- Appends only the new (non-overlapping) portion of the scrolling band to a growing list of delta segments
- Fixed chrome regions are captured once and stored separately
- A downscaled thumbnail of the strip is rendered in real-time for the live preview panel
- **Final composition** at save time: fixedTop + fixedLeft + all scroll segments + fixedRight + fixedBottom

### Memory Management

- Each delta segment stored as a separate bitmap in a list — not one massive bitmap
- Only composited into a single image at save time
- For very long scrolls, older segments can be flushed to temporary files to bound memory usage
- Duplicate/identical frames are detected and skipped

## Application Architecture

### Components

| Component | Responsibility |
|-----------|---------------|
| **App Shell** (`ScrollShot.App`) | Entry point, system tray icon, global hotkey registration, small control window, settings management |
| **Selection Overlay** (`ScrollShot.Overlay`) | Transparent topmost window spanning all monitors. Dim overlay, crosshair cursor, click-drag selection. Hosts live preview strip during scroll capture. Handles Enter/Scroll/Esc input routing. |
| **Capture Layer** (`ScrollShot.Capture`) | Wraps DXGI Desktop Duplication API. Captures a specified rectangle as a bitmap with cursor excluded. GDI fallback. Dedicated capture thread. |
| **Scroll Engine** (`ScrollShot.Scroll`) | Core stitching logic. Zone detection (4-edge). Overlap matching (row/column hashing + SAD). Incremental segment storage. Duplicate frame detection. Emits events for live preview. |
| **Preview Editor** (`ScrollShot.Editor`) | Displays stitched result. Timeline strip with free-selection cutting. Trim handles. Crop tool. Chrome toggle. Save/Copy/Discard. Undo/redo. |

### Project Structure

```
ScrollShot/
├── ScrollShot.sln
├── src/
│   ├── ScrollShot.App/          -- WPF app, entry point, tray, hotkey
│   ├── ScrollShot.Capture/      -- Screen capture (DXGI + GDI fallback)
│   ├── ScrollShot.Scroll/       -- Scroll engine: zone detection, overlap, stitching
│   ├── ScrollShot.Overlay/      -- Selection overlay window, live preview strip
│   └── ScrollShot.Editor/       -- Preview editor: timeline strip, crop, save
└── tests/
    ├── ScrollShot.Capture.Tests/
    ├── ScrollShot.Scroll.Tests/
    └── ScrollShot.Editor.Tests/
```

### Cross-Cutting Concerns

**Multi-Monitor:** Overlay spans all monitors. Capture coordinates use virtual screen space. Selection can cross monitor boundaries.

**DPI Awareness:** Per-monitor DPI-aware (v2). Capture uses physical pixels. UI scales correctly across mixed-DPI setups.

**Self-Exclusion:** The overlay uses `WS_EX_TRANSPARENT` + `WS_EX_TOOLWINDOW` to avoid being captured in screenshots. The preview strip and Done button use a separate layered window excluded from capture via `SetWindowDisplayAffinity` or by capturing before compositing the overlay.

## Preview Editor Design

### Layout

The editor adapts based on scroll direction:

- **Vertical scroll result:** Main viewport (zoomable/pannable) on the left, timeline strip on the right. Strip grows downward.
- **Horizontal scroll result:** Main viewport on top, timeline strip on the bottom. Strip grows rightward.
- **Instant capture:** No timeline strip — just the viewport with crop and save/copy/discard.

### Toolbar

Top toolbar with: Trim tool, Crop tool, Chrome toggle (On/Off), Fit/1:1 zoom, and Save/Copy/Discard buttons.

### Timeline Strip

A miniaturized representation of the entire long screenshot:

- Shows the full length of the stitched image as a proportional strip
- Chrome regions are visually distinguished (different color tint)
- Cut zones are shown as red bands with reduced opacity

### Editing Operations

**Trim Head/Tail:** Drag orange handles at the start/end of the timeline strip. The main viewport updates live to show the new boundary.

**Free-Selection Cut:** Click-drag on the timeline strip (or directly on the image in trim mode) to select any pixel range for removal. Not tied to internal segment boundaries — the user has pixel-precise control. Press Delete or click ✂️ to cut. Multiple cuts are supported, each shown as a red band on the timeline. All cuts are reversible with Ctrl+Z.

**Chrome Toggle:** The toolbar button toggles whether fixed top/bottom/left/right regions are included in the output. Useful when the user only wants the scrolling content without browser chrome, toolbars, etc.

**Crop:** Rectangular crop overlay on the main viewport. Drag corners/edges to adjust.

**Viewport Navigation:** Scroll wheel to zoom, click-drag to pan. "Fit" button fits the entire image in view. "1:1" shows actual pixels. Clicking a position on the timeline scrolls the viewport to that location.

**Undo/Redo:** Ctrl+Z / Ctrl+Y. All trim, cut, and crop operations are reversible. No destructive edits until the user clicks Save.

### Output Format

PNG only. Auto-named `ScrollShot_YYYYMMDD_HHmmss.png`, saved to a user-configurable folder (default: `Pictures/Screenshots/`).

## Settings

Minimal settings accessible from the tray icon context menu:

- **Hotkey:** configurable global hotkey (default `Ctrl+Shift+S`)
- **Save folder:** default location for saved screenshots
- **Start with Windows:** auto-start toggle

## Out of Scope (P2 / Future)

- Annotation tools (arrows, rectangles, text) — P2 feature
- JPEG/WebP output formats
- Cloud sharing / upload
- OCR / text extraction from screenshots
- Video/GIF recording
- CLI / scripting integration
