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

## Maps

Three: 3 Talwaar, Badshahi Masjid and the Faisalabad Clock Tower. One scene per
map, named `Game_<Map>`, all generated from a single table in the builder. The
builder was written around one map with its measurements as constants; more maps
meant either four copies of it or one table, and a table is the only version
where fixing something fixes it everywhere.

Each map carries its own arena centre and size, spawn and pickup ring radii,
NavMesh volume height and minimap camera height. The numbers come from `MapProbe`,
which reports a prefab's bounds, rather than from guessing. Badshahi Masjid is
154 by 282 metres, so its narrow axis caps the arena at 120 with about 17 metres
of margin either side, and its minarets reach 53 metres, so its map camera sits
higher than the others or it renders from inside one.

Nothing keeps a list of maps by hand. The build settings are populated by
scanning the scenes folder, and the menu reads its list back out of the build
settings, so a map cannot exist in one and be missing from the other — which
would be a button that fails only in a player build.

**The host decides the map.** A joiner's own selection is not a vote. Hosting
sets Mirror's `networkSceneName`, which the server sends to each client as it
authenticates, and the client loads that scene. Left unset — as it was — a
joiner simply stayed in whatever map its own menu had chosen, so hosting
Badshahi Masjid while the other player had 3 Talwaar selected put the two of
them in different cities running the same match.

**A joiner loads no map of its own.** It waits in `Connecting`, a nearly empty
scene whose only real content is a NetworkManager. A client needs one of those
to connect through, and every other one lives inside a map — which is why
joining used to mean loading whichever map the joiner had selected, connecting,
and only then being moved to the host's. Two loads, the first of them wrong.

A separate scene rather than putting the manager in the menu, which is Mirror's
textbook layout. Mirror destroys a duplicate NetworkManager's entire GameObject,
and `GameLauncher` sits on that object in every map scene, so a persistent menu
manager would kill the launcher that starts the host. This way the host path,
opening a map scene on its own, and every existing test are all untouched.

The connecting scene registers the same spawnable prefabs as a map scene,
because a client is connected and being told about spawned objects before the
host's map has finished loading.

It also gives a failed join somewhere to be reported. Before it, a client that
could not reach the host sat in a map with no players and no explanation.

**Hosts on the local network are found rather than typed.** The host broadcasts
on its own UDP port while it is up, the joiner listens, and each match found is
a row to click. Nothing depends on it: a found address is simply the address you
would otherwise have typed, and typing one is still there because broadcast does
not cross subnets, does not reach the internet, and is blocked outright on
plenty of university and office networks.

One detail that fails silently and so has a test of its own: Mirror compares a
`secretHandshake` on every discovery packet and drops anything that does not
match, and its `OnValidate` fills that field with a random number whenever it is
zero. The map scenes and the join screen are built by separate runs, so left to
themselves each side would pick its own number, no packet would ever match, and
the list would simply always be empty with nothing logged anywhere. Both sides
go through one helper that sets a fixed one, and a test asserts they agree.

The fourth landmark, Faisal Mosque, is not here. It is a bare FBX with no
prefab, no materials and no textures, and nothing in either old repository
referenced it. Making it playable is art work, not engineering.

### The texture problem

Badshahi Masjid arrived as 611MB, of which 537MB was eight 4096x4096
uncompressed BMPs. Three of those were referenced by nothing at all. Three more
already had lossless PNG twins sitting beside them. The remaining two converted
to PNG at 3.9MB and 2.9MB, because both turned out to be greyscale and write as
single channel. The map is now 67MB with nothing lost.

One trap in that conversion, worth not repeating: the BMPs were imported as
normal maps and their PNG twins were not. Repointing the materials without
fixing the import setting makes the lighting subtly wrong rather than obviously
broken, which is the kind of thing found weeks later.

## Spawn immunity and round length

Nobody can be frozen for a few seconds after arriving in a match or being sent
back to a spawn point. Without it a catcher standing near a spawn takes whoever
lands there before they have had a frame to move, which reads as the game being
broken rather than the catcher being quick.

Immunity is checked in two places on purpose. `FreezeRules` has it so the tag
query respects it, and `Freezable.Freeze` refuses independently, because that
method is public and the AI, `MatchState` and the tests all call it directly. A
rule enforced at one of two doors is not enforced.

It is granted **before** the spawn on both spawn paths, next to the role and for
the same reason: in host mode Mirror deserialises the spawn payload back onto
the same object, so a SyncVar written after spawning is overwritten by whatever
the payload said.

Immunity stops a freeze and nothing else. It deliberately does not stop a
rescue — being freed is not something anyone needs protecting from, and a runner
frozen with time left on their clock would otherwise be stuck until it ran out.
Bots get it on the same terms as people; asymmetric rules there would feel worse
than the brief pause at the start of a round.

Round length is a menu choice, from one minute to ten. It travels menu →
`MatchSetup` → `GameNetworkManager` → `MatchState`, and a map scene opened on
its own falls back to its own default, which is what keeps the scenes usable
alone.

## Settings

Volume and look sensitivity, reached from the menu.

`GameSettings` is an asset for the same reason `MatchSetup` is one — inspectable,
wired up, no static mutable state. But it is only the accessor: changes to a
ScriptableObject do not survive a build, so the values live in PlayerPrefs and
are loaded into the asset on start. Saved when the panel closes rather than on
every frame of a drag, because `PlayerPrefs.Save` writes to disk. The *effect* is
applied live as the slider moves, since volume is impossible to set sensibly if
you cannot hear the result until afterwards.

`SettingsApplier` sits in the menu, the join screen and every map. Volume has to
hold everywhere or the game would be quiet on the menu and loud the moment a
match loaded. Sensitivity only means anything where there is a camera to turn,
so it is applied where one is found and skipped where there is not.

Sensitivity captures Cinemachine's original per-axis gains once and multiplies
from them. Multiplying the current value would compound every time the slider
moved — the same trap the speed boost had to avoid — and one remembered number
would flatten the difference between axes, which Cinemachine does not give the
same gain. The axis list is discovered at runtime and the controller belongs to
the spawned player, so the applier keeps looking for it on a slow tick rather
than expecting it on the first frame.

Both values are clamped, because PlayerPrefs is a file anyone can edit. A
negative sensitivity would invert the camera with no way back except editing the
file again; zero would be a camera that will not turn at all.

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

One map first, then the rest. Freeze, unfreeze, the server-side AI and power-ups
were proven correct on 3 Talwaar before the others were ported across, so each
map was a self-contained addition rather than four sets of scene-specific
breakage arriving while the core systems were still moving.

Done: 3 Talwaar, Badshahi Masjid and Clock Tower are all playable. Faisal Mosque
is not, and cannot be without someone texturing it.

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
