# ScrollShot Implementation Task Board

| Phase | Status | Scope | Notes |
| --- | --- | --- | --- |
| Phase 0 | Completed | Solution scaffold, shared contracts, baseline test/build wiring | Solution, contracts, and baseline tests are in place |
| Phase 1 | Completed | Capture layer, scroll algorithms, compositor, editor data model | Includes GDI capture, DXGI duplication path, algorithms, edit commands, and gitignore cleanup |
| Phase 2 | Completed | Overlay UI and live preview strip | Multi-monitor helper, selection overlay, and preview strip control are in place |
| Phase 3 | Completed | Scroll engine orchestration and capture controller | Scroll sessions now produce segments, previews, and capture results through a background controller |
| Phase 4 | Completed | Preview editor UI and view model | The editor now has viewport/timeline controls, a compositing view model, and save/copy/discard workflows |
| Phase 5 | Completed | App shell, hotkey, settings, orchestration | Tray app, settings persistence, hotkey registration, and capture-to-editor orchestration are in place |
| Phase 6 | Completed | Offline stitching tooling foundation | Added dataset manifest/report schema, `slice`/`replay` CLI commands, and tooling tests for deterministic replay |
| Phase 7 | Completed | Live capture debug dump mode | Added opt-in raw frame dump capture, shared dataset schema, persisted manifests/reports, and app tests for dump output |
| Phase 8 | Completed | Real-dump stitching stabilization pass 1 | Delayed zone initialization on unusable starter frames, retried zone detection on no-match pairs, and made overlap matching less sensitive to noisy side margins |
| Phase 9 | Completed | Pluggable stitching profiles and first 1D zone experiment | Added profile-aware `ScrollSessionFactory`, wired replay to `--profile`, kept app on the default profile, and introduced a separate `signal-zone` detector for vertical-dump experiments |
| Phase 10 | Completed | Hybrid overlap experiment profile | Added `signal-hybrid` with 1D signal-guided overlap candidate search plus pixel-level refinement/fallback, expanded tests, and replayed the two real dumps for comparison |
| Phase 11 | Completed | Compositor DPI-safe export fix | Replaced DPI-sensitive bitmap drawing with pixel-exact composition, added a non-default-DPI regression test, and regenerated replay outputs so stitched images no longer contain transparent blank areas from export |

## Commits

| Phase | Commit |
| --- | --- |
| Phase 0 | `69ed823` |
| Phase 1 | `91bfd01` |
| Phase 2 | `690060e` |
| Phase 3 | `1387765` |
| Phase 4 | `961c8de` |
| Phase 5 | `0caae89` |
| Phase 6 | `505aed9` |
| Phase 7 | `1b1d6c7` |
| Phase 8 | `34699d1` |
| Phase 9 | `7a23b24` |
| Phase 10 | `7a33005` |
| Phase 11 | Current commit |

## Notes

- The implementation follows `docs\design.md` and the current stitching plan in `docs\implementation-plan.md`.
- Each phase ends with build/test verification before commit.
- The current algorithm-improvement loop now has an offline path: generate overlapping datasets from a ground-truth image, replay them through `ScrollSession`, and compare against the expected final image.
- Real scroll captures can now emit opt-in debug datasets from the app itself, including raw frames, manifest metadata, and a stitched output/report snapshot for offline analysis.
- The first real-dump stabilization pass focused on correctness over aggressive appending: defer locking zones on unusable starter pairs, retry zone detection when overlap matching fails, and compare overlaps on a stable central crop to reduce edge-noise sensitivity.
- The stitcher now has an explicit profile seam: app/runtime construction goes through `ScrollSessionFactory`, and tooling replay can switch between `current` and `signal-zone` without editing orchestrators.
- The first experiment only changes zone detection. Early replay on the two real dumps showed one dump unchanged and one dump producing a different stitched height, which is enough to validate the experiment seam but not enough yet to declare the detector better.
- The second experiment adds `signal-hybrid`, which keeps the 1D zone detector and replaces overlap search with a 1D coarse pass that ranks candidate overlaps before pixel-level refinement, with the existing matcher as a fallback.
- Replay results are now easy to compare offline across profiles. On the current two real dumps, `signal-hybrid` changed stitched heights materially (`909x2479 -> 909x2284` and `1113x1406 -> 1113x1266`), so it is producing different overlap decisions, but it is still not validated as an improvement.
- A separate export bug was uncovered while inspecting replay images: `ImageCompositor` used `Graphics.DrawImageUnscaled`, which respects source bitmap DPI metadata and could shrink drawn content, leaving large transparent areas even when the stitched segments themselves were correct.
- The compositor now draws with explicit pixel rectangles instead of DPI-sensitive unscaled drawing. After regenerating the replay outputs, every stitched image once again fills its full canvas (`content == size` for all replay folders).
