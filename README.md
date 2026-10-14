# SHIFT MEAL: NIGHTMARE DELIVERY

Cooperative-style extraction horror with physics comedy - **currently single-player only** (see Multiplayer Status).
You are an underpaid MunchCorp delivery associate sent into an abandoned fast-food facility to haul valuable
food cargo back to the loading bay before your shift ends. The Health Inspector is not happy.

## Requirements
- **Unity 6000.0.33f1** (Unity 6, "6.0"). Install via Unity Hub, with *Windows Build Support (Mono)* if you want to make a build.
- Built-in Render Pipeline (no URP asset needed - chosen so the project opens reliably with zero setup).
- No paid assets, plugins or extra packages. Only built-in Unity modules.

## Open and play
1. Extract the ZIP.
2. Unity Hub -> **Add -> Add project from disk** -> select the `ShiftMealNightmare` folder -> open with 6000.0.33f1.
   (If Hub warns the version is missing, install 6000.0.33f1 or open with a nearby 6000.0.x - not tested.)
3. On first load the project **auto-creates `Assets/Scenes/Main.unity`** and registers it in Build Settings.
   Check the Console for `[ShiftMeal] Prototype scene ready`.
4. Press **Play**. (Open `Assets/Scenes/Main.unity` if it isn't already open.)

**Repair / regenerate:** `Tools -> Shift Meal -> Generate Prototype` (safe to re-run: it keeps exactly one GameManager).
The game also bootstraps itself at runtime in *any* scene, so Play works even from an empty scene.

## Controls
| Key | Action |
|---|---|
| WASD | Move |
| Mouse | Look |
| Shift | Sprint (uses stamina, disabled when carrying > 10 kg) |
| Space | Jump |
| E | Pick up cargo / deliver at terminal |
| Q | Drop last item (Shift+Q drops all) |
| Left Mouse | Throw last item (hit the Inspector for a stun) |
| F | Flashlight (lets you see further - and be seen further) |
| R | Clock out early (at the terminal, once quota is met) |
| Esc | Pause |

## One complete shift
1. Main menu -> **CLOCK IN** -> pick a contract (Standard / Rush Hour / Graveyard) -> **ACCEPT**.
2. You spawn in the loading bay (van, green chute pad, terminal). Explore the facility.
3. Find cargo: Pizza Box ($90), Double Cheeseburger ($150), Fries Bucket ($40, found in triplets),
   Mystery Sauce Crate ($180, leaks grease while carried), Giant Combo Meal ($450, 8 kg, freezer rooms only).
   Values are multiplied by the contract.
4. Carry up to 12 kg (heavier = slower). Return to the bay: press **E** at the terminal, or just
   drop/throw the cargo on the **green chute pad** - it is auto-delivered after a moment.
5. Reach the quota before time runs out. Press **R** at the terminal to clock out early for bonus pay,
   or push your luck.
6. Avoid the **Health Inspector**: he hears footsteps (sprinting is loud), jumps, drops and crashes, sees
   you in his lamp cone, and chases. Caught = **citation**: -$100, carried cargo confiscated, sent back to the van.
   Three citations = fired.
7. Hazards: grease puddles make you slide and drop cargo (they also stun the Inspector!). Doors slide open
   when you approach. Crates and barrels are physics objects - shove them.
8. Results screen -> **NEXT SHIFT** (quota and Inspector speed rise) or **RETRY**. No Unity restart needed.

## Multiplayer status
**Not implemented.** The prototype is single-player only. No networking package is included or referenced.
What exists that would help a future port: world is built from a seed (`LevelBuilder.Build(root, seed)`),
all gameplay state lives in `GameManager`, and the player, cargo and inspector are separate components.
Netcode for GameObjects would need: networked player prefab, server-authoritative cargo/delivery/timer,
and Inspector AI running on the host only. None of that is done.

## Windows build
`Tools -> Shift Meal -> Build Windows (x64)` builds to `Builds/Windows/ShiftMeal.exe`
(or File -> Build Profiles/Settings -> Windows -> Build; `Main.unity` is already in the scene list).

## Project layout
- `Assets/Scripts/Gameplay` - GameManager (loop + UI), LevelBuilder, LevelData, Cargo, Mats (procedural materials), ProcAudio (synthesized sound), Bootstrap
- `Assets/Scripts/Player` - PlayerController
- `Assets/Scripts/AI` - Inspector (enemy)
- `Assets/Editor` - ShiftMealSetup (auto scene setup + menu commands)
- Everything visual and audible is generated procedurally at runtime; there are no external art/audio files.

## Known issues / limitations
- **This project was written without access to the Unity Editor. It has never been compiled or play-tested by the author.**
  Expect to possibly fix a compile error or two and to tune values (speeds, sensitivity, light brightness).
  Please report/inspect the Console on first import.
- Wall-sign text (TextMesh) auto-fits to its box; if signs look wrong in size, adjust `Mats.Sign`.
- Inspector pathing uses room-grid BFS plus direct pursuit; he can occasionally clip a prop corner.
- Player can't be physically blocked by other players (no multiplayer).
- Uses the legacy Input Manager (`Input.GetKey`). If you add the Input System package and set Active Input Handling
  to "Input System Package (New)" only, input will break - keep it on "Input Manager (Old)" or "Both".
- No save system. Career earnings reset when you return to the main menu.
