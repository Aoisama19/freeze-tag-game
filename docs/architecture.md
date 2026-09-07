# Architecture

## One implementation, two modes

**Everything is built on Mirror. Single-player runs as a host with no remote
clients.**

Mirror supports host mode directly, where the host is a server and a local client
in one process. Single-player is exactly that, with nobody connecting and AI
filling the other roles.

```
Single-player  →  StartHost()   →  spawn player + AI, no connections accepted
Multiplayer    →  StartHost()   →  spawn player + AI, clients connect
                  StartClient() →  connect to someone else's host
```

The difference between the two modes is which button the menu presses and whether
a human or the AI drives each character. Everything downstream (freeze, unfreeze,
power-ups, scoring, win conditions) is one implementation.

The alternative, writing single-player against plain MonoBehaviours and
multiplayer against NetworkBehaviours, means every gameplay change has to be made
twice and stay in step. It does not stay in step.

## What that forces

These are consequences of the decision above, not preferences. Getting them wrong
brings back the problem it was made to avoid.

1. **All game state lives in `[SyncVar]`s on `NetworkBehaviour`s.** If a value
   decides gameplay, the server owns it and clients are told about it.

2. **Freeze state is `[SyncVar] bool isFrozen`, never a physics layer.** Storing
   it by moving the object to a layer conflates physics configuration with game
   state and cannot replicate. Where a layer change is wanted for collision or
   rendering, it is a consequence of `isFrozen` changing, applied in the SyncVar
   hook.

3. **AI runs on the server only.** Agents are server-side decision makers that
   move networked characters. Clients never simulate AI. They receive its results
   like any other movement.

4. **Input is a request, never an action.** A client asking to freeze someone
   sends a `[Command]`. The server revalidates range and role before committing.
   Client-side range checks exist only to avoid spamming the server.

5. **No static mutable game state.** Static event classes and `Instance`
   singletons assume one game in one process. Host mode mostly survives that, but
   it makes the code lie about ownership. Instance references and server-owned
   managers instead.

## Folder layout

Project code and content stay separate from anything imported, so authorship is
never ambiguous.

```
Assets/
  _Project/
    Scripts/
      Core/          bootstrapping, game modes, scene flow
      Gameplay/      freeze mechanic, roles, rounds, power-ups
      AI/            server-side agents
      UI/            menus, HUD, minimap
      Editor/        the builders that generate the scenes
    Tests/
    Prefabs/ Scenes/ Art/ Audio/ Settings/
  ThirdParty/        Mirror
```

## Characters

One model for both sides, told apart by colour rather than by shape, so neither
side can read an advantage off a silhouette and both have the same eye height for
line of sight.

**Animation is not networked.** `CharacterAnimation` works the speed out from how
far the transform moved since the last frame, which every client already knows.
That one component covers the local player, a remote player being moved by
NetworkTransform, and an AI being moved by its NavMeshAgent, and none of the three
costs a byte on the wire.

The catch is that a teleport is indistinguishable from a sprint, and rounds begin
by teleporting everyone back to their spawn. `MoveSpeed` throws away any step too
big to have been walked.

**Root motion is off.** The motor and the agent move a character. If the animation
moved it too, the model would drift off its own collider and freezes would land on
someone who is not standing where they appear to be. The blend tree's thresholds
are still read from the clips' own travel speeds at build time, so the legs turn
over at roughly the rate the ground goes past.

The blend tree runs out of clip at the top of its range, since sprinting is
7.5 m/s and the fastest cycle available travels 5.66. Above that the run plays
back proportionally faster rather than skating, capped at 1.5x before it starts to
look like a cartoon.

Colour goes through a `MaterialPropertyBlock` rather than `renderer.material`,
which would clone the shared material once per character and leak the clone.

## Power-ups

Three of them: a speed boost, invisibility, and a clone. Six pickups sit closer to
the middle of the arena than the spawn ring, so going for one means going towards
trouble. Carry up to three, cycle with 1 and 2, use with left mouse. Bots use
theirs, but only mid chase or mid flight.

Server-authoritative throughout. The inventory is replicated state, and using one
is a request the server grants or refuses. Adding a component to the character per
power-up would not survive contact with the network, because a component added on
one machine exists nowhere else.

`PowerUpEffects` is the one place an effect has consequences, the same shape as
`Freezable`. If each power-up reached out and changed whatever it liked, then
undid that itself, anything interrupted halfway would leave the character
permanently altered.

Invisibility goes through `MapKnowledge` with the rest of the knowledge rules, so
it hides you from sight, from the minimap, and from the catcher's otherwise
unlimited view of the runners. A power-up that only removed a blip while the bot
walked straight at you would read as the game cheating.

One thing worth not rediscovering: pickups find takers with a proximity query
rather than a trigger, because nothing in this game carries a Rigidbody. Written
as `OnTriggerEnter` it works for the human, whose `CharacterController` raises
trigger events, and silently never fires for a single bot.

## The lobby

A hosted match waits before it plays. People arrive, pick a side with E, and the
host starts it with Enter. Single-player skips it, since there is nobody to wait
for and the side was chosen in the menu.

Changing sides is a request rather than an assignment. The client says what it
would like and the server checks it against `LobbyRules`, so two clients both
asking to be the catcher is settled by the server answering one of them no.

Three rules are worth stating:

* **Sides lock once a round starts.** Allowed mid round, a catcher about to lose
  could simply stop being the catcher.
* **A bot never holds the catcher seat against a person.** With bots on there is
  always an AI catcher the moment nobody human takes it, so treating that as an
  occupant would mean a host who picked Runner in the menu could never change
  their mind. The bot stands down instead.
* **A match that cannot be won will not start.** With bots off, the players
  present have to cover both sides themselves.

Nothing freezes while the lobby is open, so a catcher cannot start early. Rounds
after the first restart directly rather than returning to the lobby.

## Audio

Footsteps, a chime for freezing and thawing, pickups, power-ups, and the round
announcing itself and its result.

The sound effects are synthesised by an editor script and committed as wavs, built
the same way as the scenes and the animator, so what sits in the repository is a
readable description of them rather than opaque binaries. It also means there is
no licensing question about them at all. The exception is the footsteps, which are
Unity's own Starter Assets recordings under the Unity Companion License, kept in
`Audio/Footsteps` with the licence beside them.

`SoundBank` is an asset rather than a singleton. A static manager that looks clips
up by string at the call site turns a typo into silence, and nothing can be
checked until it is played.

Nothing about audio is networked. Every client already knows where everyone is, so
it can work out what it should be hearing. Footsteps are driven by ground covered
rather than by animation events, which means one component covers the local
player, a remote player and a bot alike.

The single exception is the power-up sound, which goes out as a `ClientRpc`. The
clone has no replicated flag of its own for a hook to fire on, so one of the three
would otherwise have been silent.

Two details worth not rediscovering. `Freezable` only chimes on an actual change,
or every character thaws audibly the moment it spawns. And footsteps reuse
`MoveSpeed`, so a round restart teleporting everyone home does not land as a burst
of running.

## Maps

Three of them: 3 Talwaar, Badshahi Masjid and the Faisalabad Clock Tower. One
scene per map, named `Game_<Map>`, all generated from a single table in the
builder. Four copies of a builder is the alternative, and a table is the only
version where fixing something fixes it everywhere.

Each map carries its own arena centre and size, spawn and pickup ring radii,
NavMesh volume height and minimap camera height. Those numbers come from
`MapProbe`, which reports a prefab's bounds, rather than from guessing. Badshahi
Masjid is 154 by 282 metres, so its narrow axis caps the arena at 120 with about
17 metres of margin either side, and its minarets reach 53 metres, so its map
camera has to sit higher than the others or it renders from inside one.

Nothing keeps a list of maps by hand. Build settings are populated by scanning the
scenes folder, and the menu reads its list back out of build settings, so a map
cannot exist in one and be missing from the other. That would be a button which
fails only in a player build.

**The host decides the map.** A joiner's own selection is not a vote. Hosting sets
Mirror's `networkSceneName`, which the server sends to each client as it
authenticates, and the client loads that scene. Left unset, a joiner simply stays
in whatever map its own menu had chosen, which puts two people in different cities
running the same match.

**A joiner loads no map of its own.** It waits in `Connecting`, a nearly empty
scene whose only real content is a NetworkManager. A client needs one of those to
connect through, and every other one lives inside a map, so without this scene
joining means loading a map, connecting, and only then being moved to the host's.
Two loads, the first of them wrong.

A separate scene rather than putting the manager in the menu, which is Mirror's
textbook layout. Mirror destroys a duplicate NetworkManager's entire GameObject,
and `GameLauncher` sits on that object in every map scene, so a persistent menu
manager would kill the launcher that starts the host. This way the host path,
opening a map scene on its own, and every test are all untouched.

The connecting scene registers the same spawnable prefabs as a map scene, because
a client is connected and being told about spawned objects before the host's map
has finished loading. It also gives a failed join somewhere to be reported,
instead of a client sitting in a map with no players and no explanation.

**Hosts on the local network are found rather than typed.** The host broadcasts on
its own UDP port while it is up, the joiner listens, and each match found is a row
to click. Nothing depends on it. A found address is simply the address you would
otherwise have typed, and typing one is still there, because broadcast does not
cross subnets, does not reach the internet, and is blocked outright on plenty of
university and office networks.

One detail fails silently and so has a test of its own. Mirror compares a
`secretHandshake` on every discovery packet and drops anything that does not
match, and its `OnValidate` fills that field with a random number whenever it is
zero. The map scenes and the join screen are built by separate runs, so left to
themselves each side would pick its own number, no packet would ever match, and
the list would always be empty with nothing logged anywhere. Both sides go through
one helper that sets a fixed value, and a test asserts they agree.

### Textures

Badshahi Masjid's textures arrived as eight 4096x4096 uncompressed BMPs totalling
537MB. Three were referenced by nothing at all. Three more already had lossless
PNG twins beside them. The remaining two converted to PNG at 3.9MB and 2.9MB,
because both turned out to be greyscale and write as single channel. The map is
67MB now with nothing lost.

One trap in that conversion, worth not repeating: the BMPs were imported as normal
maps and their PNG twins were not. Repointing materials without fixing the import
setting makes lighting subtly wrong rather than obviously broken, which is the
kind of thing found weeks later.

## Spawn immunity and round length

Nobody can be frozen for a few seconds after arriving in a match or being sent
back to a spawn point. Without it, a catcher standing near a spawn takes whoever
lands there before they have had a frame to move, which reads as the game being
broken rather than the catcher being quick.

Immunity is checked in two places on purpose. `FreezeRules` has it so the tag
query respects it, and `Freezable.Freeze` refuses independently, because that
method is public and the AI, `MatchState` and the tests all call it directly. A
rule enforced at one of two doors is not enforced.

It is granted **before** the spawn on both spawn paths, next to the role and for
the same reason. In host mode Mirror deserialises the spawn payload back onto the
same object, so a SyncVar written after spawning is overwritten by whatever the
payload said.

Immunity stops a freeze and nothing else. It deliberately does not stop a rescue,
since being freed is not something anyone needs protecting from, and a runner
frozen with time left on their clock would otherwise be stuck until it ran out.
Bots get it on the same terms as people, because asymmetric rules there would feel
worse than the brief pause at the start of a round.

Round length is a menu choice, from one minute to ten. It travels from the menu
through `MatchSetup` and `GameNetworkManager` to `MatchState`. A map scene opened
on its own falls back to its own default, which is what keeps those scenes usable
alone.

## Settings

Volume and look sensitivity, reachable from the main menu and from the in-game
menu.

`GameSettings` is an asset for the same reason `MatchSetup` is one: inspectable,
wired up, no static mutable state. It is only the accessor though. Changes to a
ScriptableObject do not survive a build, so the values live in PlayerPrefs and are
loaded into the asset on start.

Saving happens when the panel closes rather than on every frame of a drag, because
`PlayerPrefs.Save` writes to disk. The effect is applied live as the slider moves,
since volume is impossible to set sensibly if you cannot hear the result until
afterwards.

The overlay is built by one shared builder used by both menus. Two copies laid out
separately would drift the first time either gained an option, which is the same
reason the maps come from one table. In game it is built after the pause panel, so
it is the later sibling, which in UGUI is what puts it in front rather than
behind.

Escape backs out one layer at a time. Pressed over the settings it closes those
and leaves the pause menu up, rather than dropping the player back into a match
they were not looking at. Closing the pause menu closes the settings with it.

`SettingsApplier` sits in the menu, the join screen and every map. Volume has to
hold everywhere, or the game would be quiet on the menu and loud the moment a
match loaded. Sensitivity only means anything where there is a camera to turn, so
it is applied where one is found and skipped where there is not.

Sensitivity captures Cinemachine's original per-axis gains once and multiplies
from them. Multiplying the current value would compound every time the slider
moved, which is the same trap the speed boost has to avoid, and one remembered
number would flatten the difference between axes that Cinemachine does not give
the same gain. The axis list is discovered at runtime on a controller belonging to
the spawned player, so the applier keeps looking for it on a slow tick rather than
expecting it on the first frame.

Both values are clamped, because PlayerPrefs is a file anyone can edit. A negative
sensitivity would invert the camera with no way back except editing the file
again, and zero would be a camera that will not turn at all.

## Deliberately not doing

* **Client-side prediction or lag compensation.** This is a LAN and friends scale
  game. Mirror's built-in interpolation is enough. Revisit only if it actually
  feels bad.
* **Dedicated server builds.** Host mode covers both use cases.
* **Runtime avatar loading.** Services that fetch avatars at runtime pull from
  pinned Git URLs and need network access on first open, which makes the project
  fragile to open at all. Characters are committed models instead.

## Versions

| | Version | Note |
|---|---|---|
| Unity | 6000.3.23f1 (6.3 LTS) | Supported to Dec 2027 |
| Template | `com.unity.template.urp-blank` 17.0.14 | 3D URP, what 6.3 calls Universal 3D |
| URP | 17.0.1 | |
| Input System | 1.12.0 | |
| AI Navigation | 2.0.0 | NavMesh, for the server-side agents |
| Mirror | 96.11.2 | |

Two version notes worth keeping in view.

**Mirror states support through Unity 6000.1**, and this project is on 6000.3.
Nothing is known to break, since 6.3 is still Mono and .NET Standard 2.1, and the
runtime replacement that would actually threaten Mirror is CoreCLR in 6.6. But
this combination is not something Mirror validates, so if odd Weaver behaviour
shows up, suspect this first.

**Do not install Mirror from OpenUPM.** That registry is stuck well behind, by
more than a year and a security release. Use the Asset Store or the GitHub
release.

## Host mode traps

Running single-player as a host is the decision everything else rests on, and it
has sharp edges that only appear in host mode. Recorded here as they are found.

**Set spawn state before the object is spawned, never after.** In host mode Mirror
serialises an object's spawn payload inside `AddPlayerForConnection`, then hands
that payload straight back to the host client, which deserialises it onto the very
same object. See `NetworkClient.OnHostClientSpawn`. Anything written to a
`[SyncVar]` after that call is silently overwritten with its pre-spawn value.
Assigning roles after `base.OnServerAddPlayer` leaves every player a runner, so no
catcher exists and nothing can ever freeze. It reads correctly if you check
immediately after writing, because the overwrite lands a moment later.

**`[SyncVar]` hooks do not fire server-side outside host mode, and not at all for
objects outside the host client's interest range.** The condition in
`NetworkBehaviour.GeneratedSyncVarSetter` is
`NetworkServer.activeHost && !hookGuard && NetworkClient.spawned.ContainsKey(netId)`.
Side effects must therefore not live in the hook alone. Apply them through one
method called from both the hook and the server-side mutator, and make it
idempotent. `Freezable` does exactly that.

**Null-coalescing does not work on `UnityEngine.Object`.** `GetComponent<T>() ??
AddComponent<T>()` looks correct and is not. Unity overloads `==` to report a
missing or destroyed object as null, and `??` does not use that overload, so it
hands back the missing component and the next line throws. Compare with `== null`
explicitly.
