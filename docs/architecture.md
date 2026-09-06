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

## Characters

One model for both sides — Mixamo's Y Bot, carried over from the old project
along with the Unity humanoid walk, run and idle cycles. Catcher and runner are
told apart by colour, not by shape, so neither side can read an advantage off a
silhouette and both have the same eye height for line-of-sight.

Two things are worth knowing about how it is driven:

**Animation is not networked.** `CharacterAnimation` works the speed out from
how far the transform moved since last frame, which is information every client
already has. That one component covers the local player, a remote player being
moved by NetworkTransform, and an AI being moved by its NavMeshAgent, and none
of the three costs a byte on the wire. The catch is that a teleport is
indistinguishable from a sprint, and rounds begin by teleporting everyone back
to their spawn — so `MoveSpeed` throws away any step too big to have been
walked.

**Root motion is off.** The motor and the agent move a character. If the
animation moved it too, the model would drift off its own collider and freezes
would land on someone who is not standing where they appear to be. The blend
tree's thresholds are still read from the clips' own travel speeds at build
time, so the legs turn over at roughly the rate the ground goes past.

Colour goes through a `MaterialPropertyBlock` rather than `renderer.material`,
which clones the shared material once per character and leaks the clone.

## Deliberately not doing

- **Client-side prediction or lag compensation.** This is a LAN/friends-scale
  game. Mirror's built-in interpolation is enough. Revisit only if it actually
  feels bad.
- **Dedicated server builds.** Host mode covers both use cases.
- **Runtime avatar loading.** Ready Player Me and glTFast were the two most
  fragile dependencies in the old project and pulled from pinned Git URLs that
  required network access on first open. Avatars get baked to prefabs instead.

Ready Player Me is now confirmed dropped, not just proposed. The characters it
used to load at runtime are a committed model instead.

## Scope of the first pass

One map, not all four. Freeze, unfreeze, the server-side AI and power-ups get
proven correct on a single map before 3 Talwaar, Badshahi Masjid, Faisal Mosque
and Clock Tower are ported across. Each map is then a self-contained addition
rather than four sets of scene-specific breakage arriving while the core systems
are still moving.

## Versions

| | Version | Note |
|---|---|---|
| Unity | 6000.3.23f1 (6.3 LTS) | Supported to Dec 2027 |
| Template | `com.unity.template.urp-blank` 17.0.14 | "3D URP" — what 6.3 calls Universal 3D |
| URP | 17.0.1 | |
| Input System | 1.12.0 | |
| AI Navigation | 2.0.0 | NavMesh, for the server-side agents |
| Mirror | 96.11.2 (22 Aug 2026) | |

Two version notes worth keeping in view:

- **Mirror states support through Unity 6000.1**, and we are on 6000.3. Nothing
  is known to break — 6.3 is still Mono and .NET Standard 2.1, and the runtime
  replacement that would actually threaten Mirror is CoreCLR in 6.6 — but this
  combination is not something Mirror validates. If odd Weaver behaviour shows
  up, this is the first thing to suspect.
- **Do not install Mirror from OpenUPM.** That registry is stuck on 96.6.4 from
  May 2025, sixteen months and a security release behind. Use the Asset Store or
  the GitHub release.

## Host mode traps

Running single-player as a host is the decision everything else rests on, and it
has sharp edges that only show up in host mode. Recording them as they are found.

**Set spawn state before the object is spawned, never after.** In host mode
Mirror serialises an object's spawn payload inside `AddPlayerForConnection`, then
hands that payload straight back to the host client, which deserialises it onto
*the very same object* — see `NetworkClient.OnHostClientSpawn`. Anything written
to a `[SyncVar]` after that call is silently overwritten with its pre-spawn
value. Assigning roles after `base.OnServerAddPlayer` left every player a runner,
so no catcher existed and nothing could ever freeze. It reads correctly if you
check immediately after writing; the overwrite lands a moment later.

**`[SyncVar]` hooks do not fire server-side outside host mode, and not at all for
objects outside the host client's interest range.** The condition in
`NetworkBehaviour.GeneratedSyncVarSetter` is
`NetworkServer.activeHost && !hookGuard && NetworkClient.spawned.ContainsKey(netId)`.
So side effects must not live in the hook alone — apply them through one method
called from both the hook and the server-side mutator, and make it idempotent.
`Freezable` does exactly that.
