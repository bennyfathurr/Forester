# Forester implementation and verification

Delivery date: 2026-10-07. Unity 6000.6.2f1. Verification used the isolated `/tmp/ratf-unity-check` checkout; the original open project's settings were preserved. The provided pack was read in overview → architecture → mechanics → Apertus integration → handoff → schema order. It replaced the factory gameplay direction; the previous prototype remains archived in Assets/Game.

## Implemented

- Independent Forester Domain/Application/Infrastructure/Presentation/Composition/Editor assemblies. Plain C# gameplay with constructor-injected services; Unity scene references and immutable authoring copies.
- Exact 16×12 initial board, 18 build cells, NORTH/SOUTH complete polylines, intermediate path cells, two spawns and one protected forest goal. Route lengths are derived (42 and 44 units).
- Five always-visible reusable cards, configured typed direct/splash-slow/aura behavior, costs/copy limits, 120 starting PP, 200 cap, interwave awards 55/65, free atomic moves and one-time 75% paid-cost refunds.
- 20Hz movement, stable goal-distance targeting, aggregate hits, splash, refreshing slow, nonstacking aura, contained-before-leak resolution, finite spawn queues and optional bounded Gust Front. Three-wave win, integrity loss and explicit 120-second fault state.
- Untimed Preparation, cancellable Planning, three-second Telegraph, active simulation, cleanup, outcomes and pause overlay. Planning/telegraph pause handling, fresh observation on planning resume, epoch invalidation, cancellation/disposal, deadline fallback and six external wave-request limit.
- Native-asset wrappers and visual catalog, pooled entity views, elevated perspective/orthographic board camera, optional camera controls (disabled initially), Canvas HUD/card tray/details/settings/results, Input System grid raycasts, shared-validator ghost and messages, move/sell and factual interwave coverage/reflection.
- Level painter, snapped waypoint handles, path-tag export, route/mask/footprint validators and editor play guard. New card content derives from Inspector catalogs and existing combat kinds.
- Scripted, local Apertus-compatible chat-completions and authenticated owned-gateway commanders. Exact JSON parsing and full plan validation. Schema output and optional parameters are capability opt-ins. Hosted provider keys stay in gateway environment; client session token stays in memory.
- Minimal separate Python gateway: server-owned prompt/model/endpoint, host allowlist and redirect rejection, authentication, bounded bodies/deadlines, rate limiting, fake provider and initial-level validation. No hosting deployment or model download.

## Verified

| Check | Result | Evidence / practical limit |
|---|---|---|
| Standalone plain C# offline checks | 52 assertions passed | Verification/offline_tests.txt; includes placement atomicity, paid refund, footprint/caps, paths, budgets/density/conditions, splash/expiry/aura, target gaps/ties, same-tick containment, lifecycle and complete waves |
| Unity EditMode | 11 tests passed | Verification/editmode.xml; includes the offline suite, local/remote adapter contracts, strict parsing/capabilities, changed route/mask acceptance and catalog-only sixth-card acceptance |
| Unity PlayMode | 4 tests passed | Verification/playmode.xml; real scene/bindings, card callbacks, preview/errors, synthetic physical-device raycast input, pause/restart, unreachable local fallback, default three-wave survival and native-model sixth-card visual preview/paid placement/attack |
| Gateway | 18 tests passed | Verification/gateway_tests.txt; includes real authenticated loopback HTTP and 401 response, rate limit, malformed/duplicate JSON, schedule/budget rejection, bindings and provider/hostname policy |
| Scene and content validation | Passed | Build called ForesterLevelBuilder.Validate; source script GUIDs match the verification copy; all three schema copies exactly match the supplied file |
| Mac standalone | Succeeded | Builds/ForesterFinal.app; Verification/build_result.json records 0 errors and 349 nonfatal warnings. This is not a warning-clean build |
| Native player smoke | Passed for core flow | Windowed player visually inspected: original models/portraits and board, mouse card placement (120→85 PP), free move, sell (85→111 PP), Escape pause, restart, authenticated gateway probe, accepted fake-provider wave and subsequent clear/preparation |

The native smoke used the preceding compiled core build. The last menu-focus reset, provider-status wording, explicit condition preview and runtime camera wiring were recompiled and passed the final PlayMode suite; their physical-input smoke was not repeated to avoid interrupting the running player. The latest app is separately named ForesterFinal.app, while the earlier Forester.app and its current session are retained.

The three-wave default-tuning PlayMode test used ordinary initial stats and legal purchases, with accelerated simulation ticks after each real telegraph. It verifies one survival configuration; it does not establish broad balance or cross-platform bitwise determinism.

## Not verified / not deployed

The default local `http://127.0.0.1:8000/v1/chat/completions` endpoint was probed with a simple chat and was unreachable (URLError). No real Apertus inference, installed model/revision, tokenizer/chat template, schema capability, latency/memory benchmark or real hosted-provider complete wave is verified. Fake-provider success is transport/rule verification, not model inference. Production HTTPS hosting, user-account token issuance, Windows/Linux builds, optional enabled pan/zoom and dedicated audio/animation are unverified or absent.

The final build retains nonfatal warnings, including obsolete API usage in editor/test code. No unconditional package/pipeline upgrade or original asset/settings overwrite was performed. Git's settings/package diff still lists only the pre-existing ProjectSettings.asset and QualitySettings.asset changes; EditorBuildSettings, URPProjectSettings and package files are unchanged.
