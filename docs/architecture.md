# Architecture

## The problem we're solving

The previous build existed as two repositories, `Freeze_Tag_Single` and
`Freeze_Tag_Multiplayer`. They were not two features of one game — they were two
copies of the whole game, built on incompatible foundations:

| | Single-player | Multiplayer |
|---|---|---|
| Game logic | Plain `MonoBehaviour` | `NetworkBehaviour` |
| State changes | Direct `component.enabled = false` | `[Command]` → `[SyncVar]` |
| Events | Static `Actions` class | Mirror hooks |
| Freeze state | `gameObject.layer` | `[SyncVar] bool` |

Because neither could run the other's logic, every gameplay change had to be
made twice. It wasn't, and the two copies drifted: a double-freeze guard exists
in one and not the other, and the runner AI was rewritten in one and left alone
in the other. There is no single correct copy to go back to.

## The decision

**Build everything on Mirror. Run single-player as a host with no remote
clients.**

Mirror supports host mode directly — the host is a server and a local client in
one process. Single-player is simply that, with nobody connecting and AI filling
the other roles.

```
Single-player  →  StartHost()  →  spawn player + AI, no connections accepted
Multiplayer    →  StartHost()  →  spawn player + AI, clients connect
                  StartClient() →  connect to someone else's host
```

The difference between the two modes is which button the menu presses and
whether a human or the AI drives each character. Everything downstream — freeze,
unfreeze, power-ups, scoring, win conditions — is one implementation.

## What this forces on us

These are consequences, not preferences. Getting them wrong reintroduces the
original problem.

1. **All game state lives in `[SyncVar]`s on `NetworkBehaviour`s.** If a value
   decides gameplay, the server owns it and clients are told about it.

2. **Freeze state is `[SyncVar] bool isFrozen`, never a layer.** The old build
   stored it by moving the object to a `Freeze` layer, which conflated physics
   layers with game state and could not replicate. If we still want a layer
   change for collision or rendering, it is a *consequence* of `isFrozen`
   changing, applied in the `SyncVar` hook.

3. **AI runs on the server only.** Agents are server-side decision-makers that
   move networked characters. Clients never simulate AI — they receive its
   results like any other movement.

4. **Input is a request, never an action.** A client asking to freeze someone
   sends a `[Command]`. The server re-validates range and role before committing.
   Client-side range checks exist only to avoid spamming the server.

5. **No static mutable game state.** The old `Actions` static event class and
   `GameManager.Instance` singleton both assumed one game in one process. Host
   mode mostly survives that, but it makes the code lie about ownership. Prefer
   instance references and server-owned managers.

## Folder layout

Our code and content stay separate from anything imported, so authorship is
never ambiguous:

```
Assets/
  _Project/
    Scripts/
      Core/          bootstrapping, game modes, scene flow
      Gameplay/      freeze mechanic, roles, scoring
      AI/            server-side agents
      Powerups/
      UI/
    Prefabs/
    Scenes/
    Art/
    Audio/
  ThirdParty/        Mirror, Starter Assets, store packages
```

## Deliberately not doing

- **Client-side prediction or lag compensation.** This is a LAN/friends-scale
  game. Mirror's built-in interpolation is enough. Revisit only if it actually
  feels bad.
- **Dedicated server builds.** Host mode covers both use cases.
- **Runtime avatar loading.** Ready Player Me and glTFast were the two most
  fragile dependencies in the old project and pulled from pinned Git URLs that
  required network access on first open. Avatars get baked to prefabs instead.
