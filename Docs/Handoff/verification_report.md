# Verification report — 2026-10-07 (Asia/Jakarta)

## Implemented

Plain C# 20 Hz simulation, six configurable definitions, Rage/Oil, caps/reservations, construction, lane movement/targeting, simultaneous combat, child support/retreat rules, slow/Rally effects, permanent gates, shared refinery, six-minute outcomes; constructor-injected application controller and AI coordinator; scripted AI; local Ollama and HTTPS gateway adapters; strict plan parser/whitelist/budget validation; guarded action queue, slot revisions, request timeout/expiry/epoch/cancellation/fallback and 80-dispatch limit; compact observations; telemetry; dedicated authoring data/wrappers/scene; main menu/help/tutorial/settings/battle/pause/restart/results; keyboard/mouse/card deployment and pooled feedback; Python gateway/fake provider/OpenAI adapter.

Generated/new files: Assets/Game (six runtime/editor assemblies and separate test assemblies; FactoryOutskirts.unity; unit/match/level/provider/catalog assets; eight visual wrappers), Gateway, Tools/Offline, README_SETUP.md, Docs/Handoff. No package/editor/render-pipeline upgrade. Existing SampleScene and supplied model/texture/material/prefab imports were not edited by this implementation.

## Verified

| Check | Actual result | Evidence / scope |
|---|---|---|
| First offline simulation before networking | 12/12 passed | Initial headless rule checks plus a complete scripted match |
| Final headless rules/provider checks | 26/26 passed | Tools/Offline/run.sh, bundled Mono/compiler plus installed Newtonsoft |
| Unity import/compile and scene/catalog validation | Passed | Unity 6000.6.2f1 batch project copy; RATF_VALIDATION_PASS |
| Unity EditMode | 26/26 passed | Verification/editmode.xml |
| Unity PlayMode | 3/3 passed | Verification/playmode.xml: complete offline outcome/pause/restart/cleanup; injected keyboard/mouse handler; real UnityWebRequest unavailable endpoint fallback/restart |
| Gateway contract tests | 13/13 passed | python3 -m unittest discover -s Gateway -v |
| Actual gateway HTTP with fake provider | Passed | python3 Tools/Offline/gateway_smoke.py: POST 200 valid plan and unauthorized 401 |
| macOS standalone build | Passed | Unity BuildPipeline StandaloneOSX, dedicated scene, RATF_BUILD_PASS; final styling rebuild recorded below |
| Standalone UI / controls | Passed in windowed mode | Native UI inspection: menu, tutorial, actual key 1 and lane click, Emak asset/HP, paid factory turret, HUD, selection persistence, Escape cancellation/pause, restart confirmation and new match |
| Preservation | Checked | Original settings/local edits retained; two auto-added package-setting fields introduced by the open editor were removed to restore their initially clean files |

PlayMode complete-match verification advances the controller through 7,200 ticks inside Play Mode; it does not imply six real minutes of manual play. Physical keyboard/mouse interaction was separately verified in the windowed standalone app. No network is used in EditMode tests. The PlayMode endpoint failure test uses loopback port 1, a deliberately unavailable endpoint, not a live model.

## Failures found and corrected

- Initial sandboxed Unity batch launch could not create its package-manager IPC socket. Reran only the temporary project with the needed process/network permissions.
- Scene generation initially held invalid authoring-asset references across editor asset refresh/scene creation. Save/reload persistent assets before assigning composition fixed it; the final scene validates and runs.
- Unity test assembly initially omitted the installed precompiled Newtonsoft reference. Explicit precompiled DLL references fixed compilation.
- Batch editor suppressed synthetic keyboard events. The input test injects state through InputState and calls the same frame handler; actual key/lane click was subsequently verified in the standalone app.
- Native fullscreen smoke view stayed black on this host. Windowed launch (`-screen-fullscreen 0 -screen-width 1280 -screen-height 900`) rendered and accepted input. Fullscreen is not marked verified.
- Visual check found a magenta default primitive selection marker. The final source copies the already-bound URP marker material. Final player rebuild and inspection are recorded below.
- A new idle-attack test originally allowed its attacker to reach the gate during setup; corrected the fixture to keep it out of range. The test now verifies first acquisition and no banked attacks.

## Not verified / not performed

Live Ollama response: not performed; GET http://127.0.0.1:11434/api/tags failed to connect. Live hosted provider: not performed; no credentials/model ID provided. Public gateway deployment/TLS, provider model-specific schema acceptance, local warm-up throughput, cross-platform standalone builds, ultrawide/mobile/WebGL, production Canvas/accessibility/polish, balance win rate and a full six real-minute manual match are not claimed. Known limits are listed in known_issues.md.

## Commands used

- Read five Downloads documents in numeric order; inspect ProjectVersion, package manifest/cache, input/renderer configuration, scene/scripts/model inventory and git status.
- Tools/Offline/run.sh (initial simulation then expanded rules/provider checks).
- python3 -m unittest discover -s Gateway -v.
- python3 Tools/Offline/gateway_smoke.py.
- rsync copy excluding .git, Library, Temp, Logs, obj and Builds into /tmp/ratf-unity-check; preserve original editor session.
- Unity executable: /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app/Contents/MacOS/Unity.
- -batchmode -nographics -projectPath /tmp/ratf-unity-check -executeMethod RATF.Editor.FactoryLevelBuilder.BuildIfMissing -quit -logFile /tmp/ratf-unity-check-build4.log.
- -batchmode -nographics -projectPath /tmp/ratf-unity-check -runTests -testPlatform EditMode -testResults /tmp/ratf-editmode-final.xml -logFile /tmp/ratf-editmode-final.log.
- -batchmode -nographics -projectPath /tmp/ratf-unity-check -runTests -testPlatform PlayMode -testResults /tmp/ratf-playmode-final.xml -logFile /tmp/ratf-playmode-final.log.
- -batchmode -nographics -projectPath /tmp/ratf-unity-check -executeMethod RATF.Editor.FactoryLevelBuilder.BuildStandalone -quit -logFile /tmp/ratf-standalone-final.log.
- open -a [built app] --args -screen-fullscreen 0 -screen-width 1280 -screen-height 900; native UI smoke checks through Codex Computer Use.

No live provider key was read, logged, embedded or sent. No third-party asset download, package install, service publication, original scene replacement or git commit was performed.

## Final delivery result

Final source compiled and macOS BuildPipeline succeeded (`/tmp/ratf-standalone-final.log`, `RATF_BUILD_PASS`). The delivered app is **Builds/RageAgainstFactory.app** (approximately 89 MB). The final rebuild includes the URP-material selection-marker fix, dark GUI styling and explicit request lifecycle enum. Follow-up native visual inspection of these final cosmetic changes was **skipped because the Mac locked**; the earlier windowed standalone menu, actual keyboard/mouse deployment, paid AI construction, pause and restart checks passed. Do not interpret build success as proof that fullscreen or the final cosmetic appearance was rechecked.
