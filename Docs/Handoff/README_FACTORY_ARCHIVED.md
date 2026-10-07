# Rage Against the Factory — Unity prototype

Open **Assets/Game/Scenes/FactoryOutskirts.unity** in Unity **6000.6.2f1**, then press Play and choose **Start Protest**. This is a new, self-contained scene; SampleScene, original model imports, packages and project settings are preserved. The menu, settings, tutorial, battle and results are screens inside this scene. No manual inspector wiring is required.

If the scene is missing, use **Rage Against Factory → Build dedicated scene**. Existing scene replacement requires an explicit editor confirmation. Existing authoring assets/wrappers are reused. **Validate scene and bindings** checks composition and the eight required visual bindings. `RATF.Editor.FactoryLevelBuilder.BuildStandalone` builds only this scene into `Builds/RageAgainstFactory.app`, without registering scenes in the project's build settings.

## Controls and mechanics

- Cards or 1/2/3 select Emak/Bapak/Anak. Click the desired lane on the board to deploy; selection remains after deployment.
- Right-click cancels. Escape cancels selection before pausing. Space selects Rally; click a lane to commit.
- Emak is the durable adult; Bapak deals double structure damage; Anak is a noncombat supporter, has no health/damage, and retreats near robots. Its attack-speed aura does not stack.
- Rage/Oil regenerate at +6/+5 per simulation second. Purchases and Rally use ordinary balances, caps and cooldowns. No kill rewards or free starting machinery.
- Win by shutting down the shared refinery before 360 simulation seconds. Breached gates stay open. Pause stops ticks and cancels AI work. Restart disposes the previous world, requests and pooled actors.

The supplied image is a visual reference only. Its stats, weapons and apparent extra gate count do not replace the v0.2 mechanics. The presentation uses supplied 3D Kenney models on an orthographic X/Z lane board, dark HUD panels and orange/blue markers. Read [asset_mapping.md](Docs/Handoff/asset_mapping.md) for actual paths, GUIDs and disclosed stand-ins. Portrait previews exist for villagers; no supplied animation/audio was assumed. Attack/retreat/breakdown feedback is procedural and the interface cue is synthesized.

## Authoring

`Assets/Game/Data/Units/*.asset` defines six units. `Data/Levels/Match.asset` defines economy, timings and limits. `Data/Levels/FactoryOutskirts.asset` defines lane/pad coordinates. `Data/Visuals/VisualCatalog.asset` references dedicated adapter prefabs; these reference original FBX models without editing them. `Data/AI/Provider.asset` sets initial provider, endpoint, timeout, model and decision interval. Match creation copies authoring inputs into plain definitions; runtime health/resources are separate state.

The simulation uses 20 ticks/s, stable entity order and simultaneous damage. Domain and Application have no UnityEngine dependency. Infrastructure owns JSON and HTTP; presentation owns input/visuals; the composition root creates concrete services. Newtonsoft **3.2.2**, already resolved in this project, is referenced explicitly as a precompiled DLL. No packages were added or upgraded.

## Local Ollama

Run a separately installed Ollama server and install a model yourself. No model or Ollama installation is bundled or downloaded by this implementation. In Settings select **Local Ollama**, enter `http://127.0.0.1:11434/api/chat`, and enter a model ID actually installed on that server. Select **Test Connection + Schema Contract** before starting a match. It checks a real response against the same plan parser and observation validator.

Native requests send `stream=false`, a JSON schema object in `format`, the verbatim handoff prompt plus schema, and a compact observation. There is no accumulated chat transcript. Default timeout is eight real seconds; requests/plan TTL use a configurable five simulation-second cycle. Slow local models can increase `Provider.asset`'s `decisionIntervalTicks` and timeout together. For another machine, replace loopback with its reachable secured address. Desktop Unity only; WebGL is outside this MVP.

The implementation follows [Ollama structured-output documentation](https://docs.ollama.com/capabilities/structured-outputs) and [native chat documentation](https://docs.ollama.com/api/chat). The installed server/model must still be capability-probed. Local endpoint was unreachable during implementation; **live model verification was not performed**.

## Hosted gateway

The Unity client requires an **HTTPS** `/factory/decide` endpoint. Enter its gateway session token in Settings; this token stays in memory and is separate from the provider API key. Provider credentials never go into Unity assets/builds.

`Gateway/server.py` requires Python 3.10+ and the standard library only (`requirements.txt` documents this). `.env.example` contains placeholders; the server reads exported environment variables, and does not automatically load `.env`.

Run a local fake-provider gateway:

```sh
export FACTORY_PROVIDER=fake
export FACTORY_SESSION_TOKEN='choose-your-own-session-token'
python3 Gateway/server.py
```

It listens on `127.0.0.1:8080`. Put it behind a trusted HTTPS reverse proxy to use Unity hosted mode; a plain HTTP localhost service is sufficient for the supplied server smoke test. No gateway deployment or TLS certificate was provisioned.

For a live provider, set `FACTORY_PROVIDER=openai`, export `OPENAI_API_KEY` **on the server**, and set `OPENAI_MODEL` to an account-accessible model supporting the schema. The OpenAI Chat Completions adapter uses a fixed allowlisted HTTPS URL and server-owned prompt/schema with `response_format` `json_schema` and strict output. See [official OpenAI API documentation](https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create). No live provider call was made and no credentials were supplied.

The gateway authenticates requests, limits body size and request rate, validates the observation and returned plan, and returns controlled 401/429/502/504 errors. Timeout is bounded (default three seconds). The fake provider was tested through actual HTTP. The shared environment session token is an MVP authorization mechanism; a public deployment should connect it to your session issuance service and place the loopback backend behind HTTPS. Configure `PORT`/`PROVIDER_TIMEOUT` as needed. Gateway observation bounds and prices enforce the handoff defaults; update its validation together with intentional balance changes.

## Failure behavior

One request may be pending at a time. Every model plan must match request ID/schema, whitelisted actions, observed options and shared budget. Accepted purchases are queued at least one second apart and revalidated against current Oil, capacity, lifecycle, slot occupancy and slot revision. Empty plans mean wait. Stale actions are skipped without replacement. Timeout, expiry, HTTP errors or invalid JSON cause one paid scripted fallback for that cycle; late completions are discarded. Pause/restart/outcome cancels and invalidates work. After 80 model dispatches a match uses scripted AI.

## Verification and telemetry

```sh
Tools/Offline/run.sh
python3 -m unittest discover -s Gateway -v
python3 Tools/Offline/gateway_smoke.py
```

The offline runner uses the editor's bundled Mono compiler/runtime and the already-resolved Newtonsoft DLL. In Unity Test Runner, run **RATF.Tests.EditMode** and **RATF.Tests.PlayMode**. Tests use fake clocks/commanders; one PlayMode integration test uses a deliberately unavailable loopback port to exercise the real HTTP transport. Keyboard/mouse handler testing injects Input System device state because batch-editor synthetic keyboard events are suppressed. It does not establish a physical-device test.

Telemetry writes JSON lines to `Application.persistentDataPath/factory-telemetry.jsonl`: outcomes, unit usage, lane deployment counts, gate breach ticks and remaining resources. Development builds additionally include AI lifecycle/execution/rejection timing. No credentials, asset paths, player intentions or chain-of-thought are logged. Balance targets are unvalidated; tune only after collecting match data.

Read [verification_report.md](Docs/Handoff/verification_report.md) for actual passed/failed/skipped runs and [known_issues.md](Docs/Handoff/known_issues.md) for practical limits. Implemented features and verified features are deliberately reported separately.

For the supplied macOS build, run `Tools/Offline/launch_mac.sh` (or open the app with `-screen-fullscreen 0 -screen-width 1280 -screen-height 900`). Windowed mode was visually verified. The original project fullscreen settings were retained; fullscreen smoke inspection stayed black on this host and is not marked verified.
