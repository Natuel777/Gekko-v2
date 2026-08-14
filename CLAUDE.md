# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Gekko is a Unity 3D platformer (Unity **6000.0.62f1**, Universal Render Pipeline). The player controls a gecko-like
creature that climbs surfaces, uses a grappling tongue, and navigates levels with checkpoints, NPC enemies, and
collectibles. There is no CI, build script, or test suite in this repo — all work happens inside the Unity Editor.

## Working with this repo

- This is a Unity project, not something you build/run from the CLI. To verify changes compile, open the project in
  Unity Editor (6000.0.62f1) or check the generated `.csproj`/`.slnx` files after Unity regenerates them.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, and `.vs/` are Unity/IDE-generated and gitignored — never edit files
  under them, and don't treat their presence/absence as meaningful.
- `Gekko-v2.slnx` and `Gekko.slnx` are Unity-generated solution files listing the compiled assemblies. There is no
  dedicated `.asmdef` for `Assets/Scripts` — nearly all gameplay code compiles into the default `Assembly-CSharp`
  assembly. The only custom assembly definitions are `SplineTerrain.Runtime`/`SplineTerrain.Editor` under
  `Assets/Spline Terrain/`.
- No automated tests exist (`com.unity.test-framework` is a dependency but unused). Validate gameplay changes by
  reasoning through the code and, where possible, describing how to verify in the Editor — don't claim a behavior
  was tested unless it actually was.
- Scene/prefab/`.asset` files are YAML but are Unity-authored; avoid hand-editing them unless the change is a small,
  well-understood tweak (e.g. a serialized field value). Prefer making behavior changes in `.cs` files.
- Comments and log messages in the existing codebase are frequently in Spanish (e.g. `Debug.Log($"Checkpoint...")`,
  code comments explaining respawn logic). Match the existing language when editing a file that already uses it.

## Architecture

### Hybrid MonoBehaviour / plain-C#-class pattern

Most gameplay logic is **not** written directly in `MonoBehaviour` classes. Instead, a thin `MonoBehaviour` wrapper
owns a plain C# class that does the real work, and the wrapper manually forwards Unity lifecycle calls to it via
`Artificial*`-prefixed methods:

```csharp
// Player.cs (MonoBehaviour)
private void Update()  { _pjController.ArtificialUpdate(); }
private void FixedUpdate() { _pjController.ArtificialFixedUpdate(); }
private void LateUpdate()  { _pjController.ArtificialLateUpdate(); }
```

`PlayerController`, `TongueManager`, `GekkoHealth`, `GekkoCollision`, `DebugController`, `BlueberryComboTracker`,
etc. are all plain classes constructed and driven this way by `Player.cs`. When modifying player/NPC behavior, find
the plain-class implementation (usually in `Controller/` or `Model/` subfolders) rather than assuming logic lives on
the `MonoBehaviour`. When adding new per-frame logic to one of these systems, follow the same pattern — add an
`ArtificialUpdate`-style method and call it from the owning `MonoBehaviour`, don't add a new `Update()`.

### Event-driven FSM for NPCs (`Assets/Scripts/EventFSM/`, `Assets/Scripts/Interface/IState.cs`)

NPCs (`Bug`, `HeavyBeetle`, `CarnivorousPlant`) use a shared `StateMachine` class holding one `IState`. States
implement `Enter/Exit/Update/HandleEvent(CreatureEvent, object)`. Each NPC type has its own `States/` folder
(e.g. `NPCs/Bug/States/`, `NPCs/HeavyBeetle/States/`) with states named `<Creature><Behavior>State.cs`. Behaviors
(movement/attack strategies) live in sibling `Behaviours/` folders and are plain classes injected into the NPC's
`Awake()` — this is a strategy-pattern layer separate from the FSM itself. `CreatureEvent` is a single shared enum
in `IState.cs` used across all creature types, not per-NPC events.

### Global static `EventManager` (`Assets/Scripts/EventManager.cs`)

A static pub/sub bus keyed by string event names, with separate dictionaries for parameterless (`Action`) and
parameterized (`Action<T>`) events. Used for cross-system signals that don't warrant a direct reference (e.g. NPC
detection: `EventManager.Trigger<(Bug, Transform)>("OnPlayerDetected", (this, transform))`). When adding a new
cross-system signal, check here first before wiring a direct `MonoBehaviour` reference — event names are plain
strings, not enum/const-backed, so grep for the exact string when tracing subscribers.

### Singletons

`GameManager.Instance`, `ScreenManager.Instance`, `CameraStateManager.Instance`, and `UIManager.Instance` are
scene-persistent singletons (`DontDestroyOnLoad` or scene-scoped, set up in `Awake()`). `GameManager` owns the
`CheckpointManager`, the active `Player` reference (`GameManager.Instance.Pj`), and the `CollectiblesFactory`.
Gameplay code frequently reaches through `GameManager.Instance.Pj...` rather than caching player references locally.

### Checkpoints & respawn (`Assets/Scripts/Checkpoints/`)

`CheckpointManager` (owned by `GameManager`, not a `MonoBehaviour`) distinguishes **real** checkpoints (set only
when the player physically touches a `Checkpoint`, via `SaveCheckpoint`) from **debug** checkpoints (pre-registered
per numeric key via `RegisterDebugCheckpoint`, used for the 1/2/3 debug-teleport keys). `Respawn()` restarts the
scene entirely if no real checkpoint has been hit yet, rather than teleporting to an unset position — this is
intentional (resets collectibles), not a bug.

### Screens / UI stack (`Assets/Scripts/Canvas/Menu/`)

`ScreenManager` maintains a `Stack<IScreen>` of UI screens; `Push`/`Pop` call `Activate/Deactivate/Free` on screens
to manage a navigable menu stack (pause menu, options, confirm dialogs, etc.). `UIManager` (separate, under
`Canvas/InGame/`) handles in-game HUD and dialogue, and exposes `OnActivatingDialogue`/`OnDeactivatingDialogue`
events that `Player.cs` subscribes to in order to lock/unlock player control during dialogue.

### Factories & object pooling (`Assets/Scripts/Factories/`)

`Factory<T>` is an abstract `MonoBehaviour` base for spawning typed objects (`CollectiblesFactory`,
`ShooteableObjectFactory`). `ObjectPool<T>` is a plain generic pooling class (get/return/clear) used underneath the
factories — pooled objects are destroyed via `GameManager.Instance.DestroyObject`, not `Object.Destroy` directly, so
pooling continues to work if that destroy path changes.

### Data-driven NPC/collectible config: Flyweight ScriptableObjects (`Assets/Scripts/Flyweight/`)

Shared, per-type tunables (movement speeds, detection ranges, attack damage, etc.) live in `ScriptableObject`
assets (`BugDataSO`, `HeavyBeetledataSO`, `CarnivorousPlantDataSO`, `ShooteableObjectDataSO`, collectible SOs) rather
than being duplicated per-instance on prefabs. When tuning NPC behavior, edit the `.asset` data file, not per-prefab
serialized fields, unless the change is genuinely instance-specific.

### Key top-level folders under `Assets/Scripts/`

- `Player/` — `Controller/` (input, movement, aiming, interaction — plain classes driven by `Player.cs`),
  `Model/` (health, collision, tongue, combo tracking), `View/` (UI-facing display components).
- `NPCs/` — one folder per creature type, each with its own FSM `States/` and strategy `Behaviours/` subfolders.
- `Objects/` — interactable/physical world objects (bridges, platforms, collectibles, grabbable/bringable objects),
  built around `IInteractable`, `IBringgable`, `IDamageable` interfaces in `Interface/`.
- `Canvas/` — all UI, split into `InGame/`, `Menu/` (further split by screen: `Pause/`, `Win_Lose/`, `Scenes/`), and
  `SceneLoader/` (async scene loading, scene-name-to-resource lookup).
- `LevelManagers/` — per-level managers and level-boundary triggers (`DeathZone`, `FinishLevelTrigger`).
- `Dialogue/` — NPC dialogue interaction system.
- `EventFSM/`, `Interface/` — the shared FSM engine and cross-cutting interfaces described above.

## Language conventions

New code should generally follow existing per-file convention rather than a single global rule — check the file
you're editing for its comment/log language (Spanish vs English) and variable-naming style (`_camelCase` for
private fields is the dominant convention) before adding new code.
