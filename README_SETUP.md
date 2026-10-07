# Forester — Village Forest Edge

Open `Assets/Forester/Scenes/VillageForestEdge.unity` in Unity **6000.6.2f1** and press Play. The latest delivery Mac application is `Builds/ForesterFinal.app`. Exit the existing player before launching it. The earlier `Builds/Forester.app` is retained to avoid interrupting its running session. The original sample scene, assets, render pipeline, packages and project settings are preserved. The factory prototype remains in `Assets/Game`; its setup is archived in `Docs/Handoff/README_FACTORY_ARCHIVED.md`.

## Play offline

Choose Play. Start with 120 PP and select one of the five reusable cards. Click an outlined build cell; green/red preview and the text message use the same validator as the committed placement. Click an occupied cell with no card selected to inspect it, then Move or Sell. Sell refunds floor(75% of the original price). Placement, move and sell are permitted during Preparation. Start Wave requires an attacking defender, locks preparation, obtains a finite plan and previews it for three seconds. Space starts a wave; Escape pauses. Cancel Planning returns to preparation. Survive three waves to win; zero integrity loses. Interwave awards are 55 and 65 PP, capped at 200.

An example tested setup: Watch Posts at C04_R08, C08_R05 and C12_R04, plus Water Team at C06_R03. After wave 1 add Foam Station at C06_R05. After wave 2 add Water Team at C06_R08 and Water Tank at C10_R07. These are example gameplay placements, not a balance guarantee.

## Local Apertus

The client calls a **separate** OpenAI-compatible Apertus serving process. It does not download or embed a model. Default endpoint is `http://127.0.0.1:8000/v1/chat/completions`; default model is `swiss-ai/Apertus-8B-Instruct-2509`. Set the actual runtime model ID in the Inspector provider asset or the game's AI connection settings. No model-family substitution is performed.

The official [Apertus model card](https://huggingface.co/swiss-ai/Apertus-8B-Instruct-2509) describes supported serving options and a vLLM chat-completions example. Choose a runtime that supports the model architecture, tokenizer and chat template on your hardware. The vLLM example does not establish compatibility or performance on this Mac. No runtime installation or model download was performed during this implementation.

1. Start and configure your compatible model server independently.
2. Run `python3 Tools/Forester/probe_apertus.py --endpoint http://127.0.0.1:8000/v1/chat/completions --model swiss-ai/Apertus-8B-Instruct-2509`. It tests a simple chat first, then an exact wave plan and the initial level's budget rules.
3. In AI connection settings choose LocalApertus, supply endpoint/model and use Probe plan. Confirm a complete wave in Unity before treating live inference as verified.

Connection settings apply when a match starts. Restart after saving changes during pause.

Plain JSON is the default. Enable **Verified schema** only after checking the runtime's JSON-schema response format support; the CLI equivalent is `--verified-schema`. Temperature and max_tokens are opt-in capabilities (Inspector fields or Optional params toggle). Streaming is disabled. The default real-time deadline is 8 seconds. Invalid JSON, refusal, wrong binding, illegal schedule/budget or an unavailable server rejects the entire response and uses a validated scripted plan. At most six external wave requests occur per match; cancellation consumes an attempt. Running waves never replan.

The default local endpoint was probed during implementation and was unreachable. Real Apertus inference and hardware performance remain unverified.

## Owned hosted gateway

`ForesterGateway/server.py` is separate from the archived factory gateway. It uses Python's standard library and listens on loopback. For a local fake-provider check:

```sh
FORESTER_PROVIDER=fake FORESTER_SESSION_TOKEN=your-temporary-session-token PORT=8081 python3 ForesterGateway/server.py
```

Choose RemoteApertus in the game, endpoint `http://127.0.0.1:8081/forester/plan`, and the same session token. Tokens remain in memory and are never written to a provider asset or PlayerPrefs. The fake provider tests transport and validation; it is not Apertus inference.

For a real hosted provider, configure server environment variables from `ForesterGateway/.env.example`: `FORESTER_PROVIDER=apertus_compatible`, `APERTUS_ENDPOINT`, `APERTUS_ALLOWED_HOSTS`, `APERTUS_MODEL`, optional `APERTUS_API_KEY`, and verified capability flags. The server owns the endpoint, model, prompt and provider key; the client sends only the match observation. Fixed endpoint redirects are rejected. Put the loopback service behind an owned HTTPS reverse proxy for hosted use, then use its HTTPS `/forester/plan` URL in Unity. This minimal gateway uses a deployment-configured session token and a global 20 requests/minute limit; user-account token issuance and production hosting are outside this MVP.

## Authoring and asset expansion

Inspect `Assets/Forester/Data/Cards/CardCatalog.asset`, the defender/card definitions and `Data/Visuals/VisualCatalog.asset`. Add a wrapper prefab containing EntityView, a visual binding, DefenderDefinitionSO, CardDefinitionSO and entries in the CardCatalog. The tray derives from that catalog and existing typed behavior kinds handle direct attacks, splash/slow or aura. No defender-name switch must be changed. A new behavior requires a typed domain implementation. Catalog/level assets are copied into match state; PP, occupancy, effects and HP do not modify them.

Use **Forester → Level authoring painter**. Select `Data/Levels/VillageForestEdge.asset` and the card catalog. Paint cell tags, drag snapped route waypoint handles, or edit the ordered route arrays in the Inspector. After changing routes, paint intermediate route tags (overlapping build cells are reported), validate, save and rebuild the dedicated scene. Every route must terminate at its declared goal and use nonzero axis-aligned segments without cycles. Build cells must avoid all intermediate path cells, spawns and goals. Larger card footprints must have a legal fitting region. The initial level declares two complete routes with shared segments, rather than runtime path branching or carving.

**Forester → Build dedicated scene if missing** creates missing Forester content without overwriting existing authoring assets. **Rebuild dedicated scene from authored data** regenerates only the Forester scene. Build commands explicitly select that scene; the original EditorBuildSettings list is unchanged. To build via the editor, call `Forester.Editor.ForesterLevelBuilder.BuildStandalone` through batchmode in a closed project or a verification copy.

Grid colliders use layer bit 30 directly. No TagManager edits are required; if future project content uses that bit, reassign both the generator and the raycast mask together. Fixed framing is computed from board bounds with configurable perspective/orthographic camera values. Initial pan/zoom is disabled.

## Verification

```sh
sh Tools/Forester/run.sh
python3 -m unittest discover -s ForesterGateway -v
```

Unity Test Runner: Forester.Tests EditMode and PlayMode. Batch equivalents use `-runTests -testPlatform EditMode` or `PlayMode`, `-testFilter Forester.Tests`, `-testResults <path>` and `-logFile <path>`; omit `-quit` when running tests. Do not batch-open the same project while its editor is running. This delivery was verified in `/tmp/ratf-unity-check` to preserve the open project.

See `Docs/Forester/project_audit.md`, `asset_mapping.md`, `verification_report.md` and `known_issues.md`. Static character/turret/UFO models are explicitly mapped gameplay proxies. They are not water equipment or physical wildfire simulation. Missing audio/animation does not control authoritative combat.

## Forest island polish and camera

The dedicated Forester scene now has a raised slate island, moss rim, meadow patches,
stepping stones, recessed build pads, entry beacons and more of the existing Kenney trees/rocks.
Camera controls: right mouse drag to orbit (360° yaw, 25–80° pitch), middle mouse drag
or arrow keys to pan, wheel to zoom, Q/E to rotate, Home to restore the authored view.
Zoom and pan are bounded; navigation begins outside UI and never places a defender.
Placement uses pooled expanding rings; defenders pop into place and recoil/aim when firing;
shots have short impact rings. These effects do not determine simulation outcomes.
The existing two-route, three-wave level is enhanced; this does not add a new campaign.

### Runtime rendering repair

The camera now clears the full output, and the centered HUD uses Expand scaling for
widescreen displays. Combat feedback includes pooled droplets, colored impact/placement
rings, generated chimes, threat health bars and hit flashes. See
`Docs/Forester/runtime_render_fix.md` for verification and the cause of the earlier artifacts.
