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

The blend tree runs out of clip at the top of its range — sprinting is 7.5 m/s
and the fastest cycle we have travels 5.66 — so above that the run is played
back proportionally faster rather than left to skate, capped at 1.5x before it
starts to look like a cartoon.

Colour goes through a `MaterialPropertyBlock` rather than `renderer.material`,
which clones the shared material once per character and leaks the clone.

## Power-ups

Three: a speed boost, invisibility, and a clone. Six pickups sit closer to the
middle of the arena than the spawn ring, so going for one means going towards
trouble. Carry up to three, cycle with 1 and 2, use with left mouse. Bots use
theirs, but only mid-chase or mid-flight.

Server-authoritative throughout. The old build added a component to the
character for each power-up picked up and destroyed it on use, which is why none
of it survived contact with the network — a component added on one machine
exists nowhere else. Here the inventory is replicated state and using one is a
request the server grants or refuses.

`PowerUpEffects` is the one place an effect has consequences, the same shape as
`Freezable`. In the old build each power-up reached out and changed whatever it
fancied — a sprint speed, a layer, a tag, a static event — and undid it in its
own `Deactivate`, so anything interrupted halfway left the character
permanently altered.

Invisibility goes through `MapKnowledge` along with the rest of the knowledge
rules, so it hides you from sight, from the minimap **and** from the catcher's
otherwise unlimited view of the runners. A power-up that only removed a blip
while the bot walked straight at you would read as the game cheating.

One thing worth not rediscovering: pickups find takers with a proximity query
rather than a trigger, because nothing in this game carries a Rigidbody.
Written as `OnTriggerEnter` it worked for the human — a `CharacterController`
raises trigger events — and silently never fired for a single bot.

## The lobby

A hosted match waits before it plays. People arrive, pick a side with E, and the
host starts it with Enter. Single-player skips it — there is nobody to wait for
and the side was already chosen in the menu.

This is what closes the gap where only the first player into a match got a
choice of side. Changing sides is a request, not an assignment: the client says
what it would like and the server checks it against `LobbyRules`, so two clients
both asking to be the catcher is settled by the server answering one of them no.

Three rules are worth stating:

- **Sides are locked once a round starts.** Allowed mid-round, a catcher about
  to lose could simply stop being the catcher.
- **A bot never holds the catcher seat against a person.** With bots on there is
  always an AI catcher the moment nobody human takes it, so treating that as an
  occupant would mean a host who picked Runner in the menu could never change
  their mind. The bot stands down instead.
- **A match that cannot be won will not start.** With bots off, the humans
  present have to cover both sides themselves. This was the sharp edge left by
  the bot options: the host picks Runner, bots are off, and nothing is chasing.

Nothing freezes while the lobby is open, so a catcher cannot start early.
Rounds after the first restart directly rather than returning to the lobby.

## Audio

Footsteps, a chime for freezing and thawing, pickups, power-ups, and the round
announcing itself and its result.

The sounds are **synthesised** by an editor script and committed as wavs, built
the same way as the scene and the animator, so what is in the repository is a
readable description of them. That also settles the licensing: the old project's
entire audio folder was one hour-long rip of a commercial recording by a named
musician, which is neither a sound library nor ours to ship. The exception is
the footsteps, which are Unity's own Starter Assets recordings under the Unity
Companion License, kept in `Audio/Footsteps` with the licence beside them.

`SoundBank` is an asset, not a singleton. The old `AudioManager` was a static
instance with `DontDestroyOnLoad` that looked clips up by string at the call
site, so a typo was silence and nothing could be checked until it was played.

Nothing about audio is networked. Every client already knows where everyone is,
so it can work out what it should be hearing; footsteps are driven by ground
covered rather than animation events, which means one component covers the local
player, a remote player and a bot. The single exception is the power-up sound,
which goes out as a `ClientRpc`: the clone has no replicated flag of its own for
a hook to fire on, so one of the three would have been silent.

Two details worth not rediscovering: `Freezable` only chimes on an actual change,
or every character thaws audibly the moment it spawns; and footsteps reuse
`MoveSpeed`, so a round restart teleporting everyone home does not land as a
burst of running.

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
