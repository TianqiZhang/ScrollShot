# Crop-Centric Editor Redesign

## Problem

The current editor uses a dual-control model: an image viewport (pan/crop modes) and a timeline strip (trim/cut modes). The timeline strip treats the stitched screenshot as a sequence of segments, but from the user's perspective it's just an image. This creates:

- Disconnected controls (edits on the timeline, preview in the viewport)
- Mode-switching friction (4 modes across 2 controls)
- Trim flooding the undo stack (one entry per mouse-move pixel)
- A vertical sidebar timeline that's unusual UI for an image editor
- Trim is just a constrained crop; the abstraction doesn't earn its complexity

## Design

Replace the timeline strip with a crop-centric viewport where crop handles are always available. Cut stays as a secondary tool.

### Layout

```
┌─────────────────────────────────────────────────────┐
│ [Undo] [Redo]  [Chrome ☑]  [Zoom - 100% + Fit 1:1] │  toolbar
├─────────────────────────────────────────────────────┤
│                                                     │
│   Image viewport with always-on crop handles        │
│   ┌──○────────────────────────────○──┐              │
│   │                                  │   dimmed     │
│   ○        kept region               ○   outside    │
│   │                                  │              │
│   └──○────────────────────────────○──┘              │
│                                                     │
├─────────────────────────────────────────────────────┤
│ 1920×4320 → 1800×3200 │ [Cut Band] │ [Save] [Copy] │  status bar
└─────────────────────────────────────────────────────┘
```

### Components

#### 1. Toolbar (top bar)

Single horizontal bar containing:
- **Undo / Redo** buttons (left)
- **Chrome** checkbox toggle: "Window Frame" with a CheckBox, not a Button (center-left)
- **Zoom controls** (right): [-] [percentage] [+] [Fit] [1:1]

No mode toggle buttons. No "Image" / "Sequence" / "Edits" section labels.

#### 2. Image Viewport (center)

The viewport fills all available space between toolbar and status bar. No inspector panel, no timeline strip.

**Pan behavior:**
- Left-click drag on the image (outside any crop handle) pans
- Mouse wheel zooms toward the cursor position (anchor-point zoom)
- Right-click drag or middle-click drag always pans regardless of crop state

**Crop behavior (always available):**
- Click and drag on empty image area to draw a new crop rectangle
- When a crop is active, 8 handles appear: 4 corners + 4 edge midpoints
- Drag a handle to resize the crop
- Drag inside the crop rectangle to reposition it
- Area outside the crop is dimmed with a semi-transparent overlay
- The crop rectangle has a 2px blue border (accent color)
- Handles are 10x10px squares, accent-colored, positioned at corners and edge midpoints

**Concurrent pan + crop:**
- When a crop rectangle exists, left-click drag inside it repositions the crop
- Left-click drag outside the crop (but not on a handle) draws a new crop, replacing the old one
- Mouse wheel always zooms (toward cursor), regardless of crop state
- Right-click drag or middle-click drag always pans

**Crop coordinates:**
- Crop rectangle snaps to integer pixel boundaries
- Minimum crop size: 2x2 pixels (below this, the crop is discarded)

#### 3. Status Bar (bottom bar)

Single horizontal bar containing:
- **Dimensions text** (left): shows current output size, e.g., "1920 x 4320 px" or "1920 x 4320 -> 1800 x 3200 px" when cropped/edited
- **Edit summary** (center-left): brief text like "Crop 1800x3200 . 1 cut . Chrome removed"
- **Cut Band** button (center): only visible when `IsScrollingCapture` is true. Activates the cut interaction.
- **Save Image** button (right): primary styled
- **Copy Image** button (right): secondary styled

#### 4. Cut Band Interaction (secondary tool)

Available only for scrolling captures via the "Cut Band" button in the status bar.

**Activation:**
- Click "Cut Band" button. The button stays pressed/highlighted. Cursor changes to crosshair.

**Drawing the cut:**
- Click and drag vertically (for vertical scrolls) or horizontally (for horizontal scrolls) across the viewport to define the band to remove
- A red semi-transparent overlay shows the band being defined
- The band spans the full width (vertical scroll) or full height (horizontal scroll) of the image

**Confirming/canceling:**
- Release mouse to confirm the cut. The band is applied immediately via CutCommand.
- Press Escape before releasing to cancel
- After confirming, the "Cut Band" button returns to its default (unpressed) state

**Existing cuts:**
- Applied cuts are visible as red semi-transparent bands on the viewport
- To remove a cut, use Undo

#### 5. Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+S | Save Image |
| Ctrl+C | Copy Image |
| Escape | Clear crop (if active), or cancel cut band (if active) |

### What Changes

#### Removed
- `TimelineStrip.xaml` + `.xaml.cs` — entire control deleted
- Inspector panel (right sidebar with save/workflow tips/edit summary cards)
- Pan/Crop mode toggle buttons
- Trim/Cut mode toggle buttons
- Trim concept at the UI level (trim is now just crop from the edges)
- Header section (title/subtitle/state card)
- `_viewportMode` and `_timelineMode` state tracking in code-behind

#### Modified
- **`PreviewEditorWindow.xaml`** — new three-row layout: toolbar, viewport, status bar. No columns.
- **`PreviewEditorWindow.xaml.cs`** — simplified. No mode management. Wire keyboard shortcuts via InputBindings. Wire cut band button.
- **`ImageViewport.xaml`** — add crop handle elements, dimming overlay rectangles
- **`ImageViewport.xaml.cs`** — rewrite interaction: always-on crop with handles, anchor-point zoom, concurrent pan+crop, cut band mode
- **`PreviewEditorViewModel.cs`** — minor: remove `Direction`/`IsVerticalDirection`/`HasTimeline` properties used only for timeline layout. Keep `Direction` internally for cut band axis detection. Add keyboard-bound commands.

#### Unchanged
- `EditCommandStack` + `IEditCommand` interface
- `TrimCommand`, `CutCommand`, `CropCommand`, `ChromeToggleCommand`
- `EditState`, `CropRect`, `CutRange`, `TrimRange` models
- `ImageCompositor` and composition pipeline
- All existing tests (they test the command/model layer, not the UI)
- `CaptureResult`, `ScrollSegment`, and all upstream capture/stitch code

### Zoom-to-Cursor Implementation

Current zoom changes the ScaleTransform uniformly from the top-left origin. The fix:

1. Before zoom: record the mouse position in viewport coordinates and the corresponding scroll offset
2. Apply the new scale
3. After zoom: calculate the new scroll offset needed to keep the same image pixel under the cursor
4. Set the new scroll offset

Formula:
```
newOffset = (mousePositionInViewport + oldOffset) * (newScale / oldScale) - mousePositionInViewport
```

### Crop Handle Hit-Testing

When the user clicks on the viewport, determine the action:
1. If within 6px of a crop handle center -> resize drag (that handle)
2. If inside the crop rectangle -> reposition drag
3. If outside the crop rectangle -> new crop drag (replaces existing)

Handle positions are recalculated on every zoom/pan/resize to stay pixel-accurate.

### Dim Overlay

When a crop is active, four rectangles fill the area outside the crop box with a semi-transparent dark overlay (rgba 0,0,0,0.4)). This uses the classic "four-rectangle" approach:
- Top band: full width, from image top to crop top
- Bottom band: full width, from crop bottom to image bottom
- Left band: crop height, from image left to crop left
- Right band: crop height, from crop right to image right
