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
| Phase 12 | Completed | Synthetic fixture generator and baseline tests | Added a `synthesize` tooling command for deterministic fixed-header/footer scroll fixtures, added behavior tests for zone detection / overlap / replay, and verified the current profile is nearly lossless on a basic synthetic dataset |
| Phase 13 | Completed | Direction-aware fixed-zone hardening | Made the default `ZoneDetector` respect known scroll direction so vertical captures only infer top/bottom fixed zones, added regressions for stable dark side margins and footer-only synthetic captures, and replayed the two real dumps to confirm the spurious left/right fixed zones disappeared |
| Phase 14 | Completed | Conservative zone refinement after lock | Updated `ScrollSession` so later zone refinements are only adopted when they still produce a valid overlap and only shrink already-locked fixed regions, preventing late noisy frames from expanding fixed zones while still allowing false fixed edges to be corrected |
| Phase 15 | Completed | Multi-frame zone re-estimation and history rebuild | `ScrollSession` now aggregates usable adjacent-pair zone observations across the full frame history, rebuilds segments from the aggregated zone, and keeps the current synthetic + real-dump baselines stable while allowing vertical fixed-side detection experiments |

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
| Phase 11 | `9989e47` |
| Phase 12 | `d496370` |
| Phase 13 | `eeef2db` |
| Phase 14 | `830efa0` |
| Phase 15 | Uncommitted by request |

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
- The tooling now includes a dedicated synthetic dataset generator instead of relying only on sliced long images. It renders fixed header/footer chrome and a deterministic scrollable body, then emits ground truth plus per-frame captures where only the scroll band moves.
- The new synthetic baseline tests validate three things on generated data: fixed-zone detection, overlap detection, and end-to-end replay. On the baseline synthetic fixture, the current algorithm replayed with a normalized difference of about `0.000041` to ground truth, which is close enough to treat the basic path as working while leaving room for future stricter metrics.
- The latest hardening pass keeps the default detector aligned with the known scroll direction: vertical captures now only scan for fixed top/bottom zones, which removed the false left/right fixed margins on both real vertical dumps. The footer-only dump still produces a small false top zone (`top=9`), so the next detector iteration should focus on multi-frame confidence or minor-edge suppression rather than side-edge noise.
- The next pass moved the remaining correction into `ScrollSession`: once a zone is locked, later frames may only refine it if the refined band still produces a valid overlap, and the accepted refinement is conservative (it can shrink fixed edges but not expand them from single-frame noise). That keeps the synthetic baseline exact again while improving the footer-only real dump from `top=9,bottom=120` to `top=0,bottom=120`.
- The current pass replaces single-step zone lock/refine behavior with history-based estimation: as more frames arrive, the session re-evaluates adjacent pairs, aggregates usable zone candidates, and rebuilds overlap/segments from that global estimate. Validation stays green (`ScrollShot.sln`, scroll tests, tooling tests), the no-fixed real dump still ends at `top=0,bottom=0,left=0,right=0`, and the footer-only dump still settles at `top=0,bottom=120`; the remaining gap is not stability anymore, but choosing between competing trailing-edge candidates like `120` vs `153`.
