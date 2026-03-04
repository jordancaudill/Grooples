# Groople – Unity Game

Cooperative multiplayer physics-based 2D platformer with a grapple mechanic. Up to 8 players connect via Unity Relay join codes and complete levels together.

## Tech Stack

- **Engine:** Unity (URP, 2D)
- **Networking:** Fish-Networking (FishNet) — client-server architecture
- **Relay:** Unity Relay + Unity Authentication (anonymous)
- **Transport:** Fishy Unity Transport (UTP); WebSockets for WebGL, DTLS for Standalone
- **Input:** Unity Input System
- **Camera:** Cinemachine 3
- **UI:** TextMesh Pro + UI Toolkit

## Project Structure

```
Assets/
  Scripts/          # All game scripts
  Scenes/           # Lobby.unity, Level1–Level5.unity
  Prefabs/          # NetworkManager, MultiplayerManager, Player, LobbyPlayer, GameManager, Finish
  FishNet/          # FishNet framework (do not modify)
Packages/
  manifest.json
```

## Key Scripts (`Assets/Scripts/`)

| File | Role |
|---|---|
| `MultiplayerManager.cs` | Singleton `NetworkBehaviour`. Owns `SyncList<Player>` and `SyncVar<string> joinCode`. Spawns player prefabs for lobby and levels. |
| `PlayerController.cs` | `NetworkBehaviour` with FishNet prediction. Owns physics (`PredictionRigidbody2D`), jumping, and reconciliation. |
| `PlayerMovement.cs` | `NetworkBehaviour`. Handles Input System events, grapple raycast + SpringJoint2D, out-of-bounds teleport, sound SFX RPCs. |
| `Tongue.cs` | `NetworkBehaviour`. LineRenderer animation showing grapple direction/hit/miss state. |
| `UIController.cs` | Pause menu, volume slider, success screen, scene transitions for gameplay levels. |
| `LobbyUIController.cs` | Host/join via Unity Relay, anonymous auth, color picker UI. |
| `LobbyPlayer.cs` | `NetworkBehaviour`. Per-player lobby card showing color and "Me" label. |
| `Finish.cs` | `NetworkBehaviour`. Detects all players on finish platform for 3 seconds → triggers success. |
| `PlayerColors.cs` | Static palette of 8 colors (body + tongue hex values). |

## Architecture

### Network Object Hierarchy
```
NetworkManager (always present)
└── MultiplayerManager (spawned on server start)
    ├── Player prefabs (one per client, game scenes)
    └── LobbyPlayer prefabs (one per client, lobby scene)
```

### Scene Flow
1. **Lobby** — players connect, pick colors, host shares join code
2. **Level 1–5** — all players load together; finish platform triggers next level
3. Returning to lobby resets player prefabs

### FishNet Patterns Used
- **Prediction/Replication** — `PlayerController` uses `[Replicate]` + `[Reconcile]` with `PredictionRigidbody2D` for lag-free movement
- **ServerRpc + ObserversRpc** — SFX events use `RunLocally = true` eager pattern
- **SyncVar / SyncList** — `joinCode` and `_players` auto-sync across all clients
- **SceneManager** — network-aware scene loading (all clients load together)

### Player Data Model
```csharp
public class Player {
    public int clientId;
    public string color; // "pink" | "green" | "yellow" | "purple" | "teal" | "red" | "blue" | "orange"
}
```

## Gameplay Constants

| Parameter | Value |
|---|---|
| Max players | 8 |
| Ground move speed | 5 units/s |
| Hang move speed (grappling) | 15 units/s |
| Jump force | 3 (impulse) |
| Grapple max distance | 10 units |
| Grapple cooldown | 0.25 s |
| Finish hold time | 3 s |
| Physics layers | Terrain = 3, Players = 7 |

## Input

Uses the New Input System. Actions: `OnMove`, `OnLook` (aim), `OnGrapple`, `OnJump`. Supports Keyboard/Mouse and Gamepad. UI icons switch dynamically based on active control scheme.

## Adding New Features

- **New networked behaviour** — extend `NetworkBehaviour`, use `[ServerRpc]`/`[ObserversRpc]` for server↔client calls
- **New player movement** — modify `PlayerMovement.cs`; update `ReplicateData` struct in `PlayerController.cs` if new input state is needed for prediction
- **New level** — duplicate an existing level scene, add to build settings, update `UIController.LoadNextLevel()` scene index logic
- **New color** — add entry to `PlayerColors.cs` and update color cycle length in `MultiplayerManager`

## Common Gotchas

- **Prediction:** Never directly set `Rigidbody2D` values in `PlayerController`; always go through `PredictionRigidbody2D` so reconciliation works correctly
- **Server-only logic:** Prefab spawning must run on the server only (`IsServerStarted` guard)
- **WebGL build:** Relay transport must use WebSockets (`wss`) — this is conditional in `LobbyUIController`
- **SyncList callbacks:** Use `OnChange` callbacks rather than polling `_players` each frame
- **Scene loading:** Always use FishNet's `SceneManager` for scene transitions, not `UnityEngine.SceneManagement.SceneManager`, so all clients stay in sync
