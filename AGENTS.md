# AGENTS.md

This file provides guidance to AI coding agents when working with code in this repository.

## Build & Test Commands

```bash
# Build the entire solution
dotnet build ScrollShot/ScrollShot.sln

# Run all tests
dotnet test ScrollShot/ScrollShot.sln

# Run tests for a specific project
dotnet test ScrollShot/tests/ScrollShot.Scroll.Tests/ScrollShot.Scroll.Tests.csproj

# Run a single test by name
dotnet test ScrollShot/ScrollShot.sln --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Run the CLI tooling (replay, slice, synthesize)
dotnet run --project ScrollShot/src/ScrollShot.Tooling -- replay --manifest <manifest.json> --output <output-folder> [--profile current|signal-zone|signal-hybrid|bidirectional-current]
dotnet run --project ScrollShot/src/ScrollShot.Tooling -- slice --input <image> --output <output-folder> --viewport-height <px>
dotnet run --project ScrollShot/src/ScrollShot.Tooling -- synthesize --output <output-folder> --viewport-height <px>
```

## Architecture

**ScrollShot** is a Windows WPF desktop app (.NET 8) that captures scrolling content and stitches it into a single long screenshot. The solution is at `ScrollShot/ScrollShot.sln`.

### Project Dependency Graph

```
ScrollShot.App (WinExe, entry point)
├── ScrollShot.Overlay → ScrollShot.Capture, ScrollShot.Scroll
├── ScrollShot.Editor → ScrollShot.Scroll
├── ScrollShot.Capture
├── ScrollShot.Scroll
└── ScrollShot.StitchingData

ScrollShot.Tooling (CLI, offline analysis)
├── ScrollShot.Capture, ScrollShot.Scroll, ScrollShot.Editor, ScrollShot.StitchingData
```

### Key Modules

- **ScrollShot.Capture** — Screen capture via DXGI Desktop Duplication (primary) or GDI BitBlt (fallback). `ScreenCapturerFactory` auto-selects.
- **ScrollShot.Scroll** — Core stitching engine. `ScrollSession` orchestrates zone detection, overlap matching, and segment assembly. Algorithms live in `Algorithms/` (ZoneDetector, OverlapMatcher, RowColumnHash, PixelBuffer). Experimental variants in `Experiments/`.
- **ScrollShot.Overlay** — Transparent topmost WPF window for area selection and live preview during scroll capture.
- **ScrollShot.Editor** — Preview editor with zoomable viewport, timeline strip, trim/cut/crop editing (command pattern with undo/redo via `EditCommandStack`), and `ImageCompositor` for final output.
- **ScrollShot.App** — App shell with system tray, global hotkey (Ctrl+Shift+S), settings persistence, and `CaptureOrchestrator` workflow (hotkey → overlay → capture → editor).
- **ScrollShot.StitchingData** — Dataset manifest/report models shared between app debug dumps and offline tooling.
- **ScrollShot.Tooling** — CLI for offline replay of captured datasets through `ScrollSession`, synthetic dataset generation, and image slicing.

### Capture-to-Stitch Pipeline

1. `CaptureController` runs a background capture loop producing frames
2. `ScrollSession` processes each frame sequentially:
   - Frame 1: store as reference
   - Frame 2: detect fixed zones (top/bottom bands that don't scroll)
   - Frame N: match overlap between previous and current scroll band, extract delta, append segment
   - Periodically re-estimates zones using multi-frame history aggregation
3. `ImageCompositor` composites segments into the final image at save time (pixel-exact, DPI-safe)

### Stitching Profiles

Four pluggable algorithm profiles selected via `ScrollSessionFactory`:
- `current` — default production profile
- `signal-zone` — experimental 1D signal-based zone detector
- `signal-hybrid` — 1D signal zone detection + signal-guided overlap search
- `bidirectional-current` — experimental bidirectional overlap selection + signed placement

The app always uses `current`. Tooling replay supports `--profile` for A/B comparison.

### Offline Testing Infrastructure

The `dumps/` directory contains real captured debug datasets. The tooling CLI can:
- **replay** datasets through any profile and produce stitched output + report
- **synthesize** deterministic scroll fixtures with known ground truth
- **slice** existing long screenshots into frame sequences

This enables algorithm development without live scrolling.

## Design Documents

- `docs/design.md` — Full product design spec (interaction flows, algorithm details, component architecture)
- `docs/implementation-plan.md` — 15-phase implementation roadmap with dependency matrix
- `docs/task-board.md` — Phase completion tracking with commit references and stabilization notes
