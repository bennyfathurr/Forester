# Runtime rendering repair — 2026-10-07

The previous polish delivery was not visually verified and the user reported corrupted
bands around the camera view and clipped HUD content in Unity Play mode.

Root causes addressed:
- The only base camera used a partial viewport. Pixels outside it were not cleared by
  this scene; on the reported runtime they displayed stale/undefined frame contents.
  Both scene generation and runtime initialization now use a full-screen solid-color camera.
- Fixed 1280x900 UI coordinates with a blended CanvasScaler mode could exceed the logical
  screen height on widescreen displays. A centered reference-size container and Expand
  scaling keep the entire HUD inside the output area.
- Camera help overlapped selected-unit details; moved below the top status area.
- Planning text persisted into Active; phase transitions now update the message.

Feedback changes:
- Bounded pools of expanding rings and six animated droplets per burst; actual material
  property colors instead of relying on vertex colors ignored by the unlit shader.
- Short synthesized containment chime, rate limited. No source audio asset was modified.
- Threat health bars facing the camera, hit flash, cosmetic departure bursts.
- Attack events are collected across every simulation tick in one presentation frame,
  so a multi-tick frame does not erase earlier attacks' feedback.
- Raised the camera composition to leave more space for the lower HUD.

Verification is recorded in Verification/runtime_final_playmode.xml and
Verification/runtime_final_build_result.json when complete. Native inspection of the
initial repaired build at 1600x900 showed clean full-frame rendering and visible cards.

Final verification:
- Offline suite: 52 assertions passed.
- Unity PlayMode: 7/7 passed, including full viewport/HUD bounds, feedback tint/pool cleanup,
  camera picking/reset, full three-wave simulation, placement and provider fallback.
- macOS build: succeeded, 0 errors, 349 nonfatal warnings.
- Native final player at 1600x900: menu, full-screen clear, full card tray, actual mouse
  deployment of three Watch Posts (120 -> 45 PP), live health bars, zoom/keyboard rotation,
  wave-one completion (8 contained / 0 leaked / integrity 20, PP 100), and preparation
  for wave two directly observed. No game exceptions found in the session log.
- Fast synthetic clicks were intermittent; held clicks via a one-pixel drag worked.
  This does not establish physical right-drag orbit or audible sound quality; those are
  implemented, while camera behavior and feedback initialization have automated coverage.
- Final player: Builds/ForesterVerified.app. Its bundle identifier/display name differ only
  to distinguish it from older open players; project settings and game content are unchanged
  by that packaging step. Builds/ForesterFixed.app contains the same game binaries under
  the original application identity. Earlier intermediate builds are not the final delivery.
