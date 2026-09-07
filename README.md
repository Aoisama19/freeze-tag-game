# Baraf-Paani

A multiplayer freeze-tag game set in three Pakistani landmarks. One player
catches, everyone else runs; a frozen runner stays frozen until a team-mate
reaches them. Play alone against bots, or host a match and let friends on the
same network find it in a list.

Built in Unity 6.3 LTS on Mirror. This is a rebuild of a BSc final-year project
that previously existed as two separate, diverging codebases — one single-player,
one multiplayer. Here there is one game that runs both ways.

![The main menu](docs/screenshots/menu.png)

## The game

A round runs for as long as you choose, one to ten minutes.

- **The catcher** freezes runners by touching them. They know where every runner
  is, always — on a 120-metre map a catcher who has to hunt by sight plays as
  broken rather than fair.
- **Runners** stay alive by not being touched, and free frozen team-mates by
  reaching them. They see their team-mates at all times but only see the catcher
  while the catcher is actually in view, so breaking line of sight is worth
  doing.
- **The catcher wins** by freezing everybody. **The runners win** by having
  somebody still free when the clock runs out.

Nobody can be frozen for the first few seconds of a round, so a catcher standing
near a spawn point cannot take people before they have moved. The HUD says how
long you are still safe for.

The score carries across rounds within a match.

### Power-ups

Six pickups sit around the middle of each arena — closer in than the spawn ring,
so going for one means going towards trouble. Carry up to three.

- **Speed** — considerably faster for a few seconds, for running away or running
  somebody down.
- **Invisible** — off everyone's screen, off the minimap, and out of the
  catcher's otherwise unlimited view of the runners.
- **Clone** — leaves a standing copy of you wearing your role, so the catcher has
  to pick. Tagging it appears to work.

Bots use theirs too, but only when it counts: mid-chase, or mid-flight.

## Controls

| | |
|---|---|
| Move | `W` `A` `S` `D` or the arrow keys, left stick |
| Look | Mouse, right stick |
| Sprint | `Left Shift`, left stick press |
| Jump | `Space` |
| Use the selected power-up | Left mouse, `Enter` |
| Cycle power-ups | `1` and `2`, D-pad left and right |
| Switch side (in the lobby) | `E` |
| Start the match (host, in the lobby) | Left mouse, `Enter` |
| Menu — resume, settings, leave, quit | `Escape`, gamepad Start |

Volume and look sensitivity are under Settings, reachable from the main menu and
from the in-game menu. They are remembered between sessions.

## The maps

Three, chosen from the menu, each showing a picture rendered from the map itself.

**3 Talwaar** — a Lahore roundabout and the streets around it.

![3 Talwaar](docs/screenshots/3talwaar.png)

**Badshahi Masjid** — the mosque's courtyard, water channels and gate.

![Badshahi Masjid](docs/screenshots/badshahimasjid.png)

**Faisalabad Clock Tower** — the eight bazaars radiating from the tower.

![Faisalabad Clock Tower](docs/screenshots/clocktower.png)

Each map is a 120-metre square cut out of a much larger model. The playable
square, the spawn ring, the power-up ring and the minimap framing are all per-map
numbers in one table in `PlayableSceneBuilder.cs`.

## Playing together

**Hosting.** Pick a side, a map and a round length, then *Host a match*. The
match waits in a lobby until you start it. Anyone joining picks their own side
with `E`; there can only be one catcher, and a bot will not hold that seat
against a person.

**Joining.** *Join* opens a screen listing matches being hosted on your network.
Click one and you go straight into whatever map the host chose. If the list is
empty, type an address instead — that always works, and broadcast does not:
it does not cross subnets, does not reach the internet, and is blocked outright
on a good many university and office networks. A firewall prompt dismissed on the
host's machine will also leave it invisible.

**Bots** fill any empty runner slots and step aside as people arrive. They can be
switched off entirely, in which case the humans present have to cover both sides
— a match with nobody catching will refuse to start rather than run a round
nobody can win.

## Running it from source

Requires **Unity 6000.3.23f1** and **Git LFS** — the models and audio are stored
through LFS, so a clone without it gets pointer files instead of assets.

```
git lfs install
git clone <this repository>
```

Open the folder in Unity Hub, then open `Assets/_Project/Scenes/MainMenu.unity`
and press Play.

Any map scene also runs on its own without the menu — open one and press Play and
it starts a single-player match. That is deliberate, and it is what the tests
rely on.

### The scenes are generated

The menus and the map scenes are built by editor scripts rather than assembled by
hand, so how they are put together is readable and reviewable instead of being
inspector state nobody can diff. Under the **Baraf-Paani** menu in the editor:

| | |
|---|---|
| Rebuild Playable Scene | the character prefabs and all three map scenes |
| Rebuild Main Menu | the menu |
| Rebuild Connecting Scene | the join screen |
| Rebuild Character Animator | the locomotion blend tree, from the clips |
| Rebuild Sounds | synthesises the sound effects |
| Rebuild Map Previews | renders the map pictures the menu shows |
| Probe Maps | reports each map's bounds, for setting arena numbers |

Running one replaces its output from scratch. Editing those scenes by hand works,
but the next rebuild will overwrite it.

## Tests

102 EditMode and 74 PlayMode tests.

```
Unity.exe -runTests -batchmode -nographics -projectPath . -testPlatform EditMode
Unity.exe -runTests -batchmode -projectPath . -testPlatform PlayMode
```

**PlayMode must run without `-nographics`.** The minimap renders to a texture,
and without a graphics device that fails and takes most of the suite with it.

The EditMode tests cover the rules as pure functions — who may freeze whom, what
the lobby allows, how a round resolves. The PlayMode tests stand up real matches:
every map is loaded, hosted and played to check it has a camera, a baked NavMesh,
spawn points off the rooftops, and bots that are actually on the mesh.

Screenshots are taken by a play-mode tool that is skipped unless asked for:

```
Unity.exe -runTests -batchmode -projectPath . -testPlatform PlayMode -captureScreenshots
```

## Building and sharing it

```
Unity.exe -quit -batchmode -nographics -projectPath . -buildWindows64Player Builds/BarafPaani.exe
```

A Windows build is **not a single file**. `BarafPaani.exe` needs
`BarafPaani_Data/`, `UnityPlayer.dll` and `MonoBleedingEdge/` beside it, so
anything shared has to be a zip of the whole `Builds` folder — the bare .exe will
not start.

Builds are not committed. They are tens of megabytes, change wholesale every
time, and git keeps every version for ever; `Builds/` is in `.gitignore`. Attach
the zip to a GitHub release instead, or put it somewhere like itch.io.

Windows SmartScreen will warn about an unsigned executable downloaded from the
internet. That is expected for an unsigned build and not a sign of anything
wrong, but it is worth telling people before they see it.

## How it is put together

```
Assets/_Project/
  Scripts/
    Core/        starting and joining matches, what the menu chose
    Gameplay/    freezing, roles, rounds, power-ups — the rules
    AI/          what a bot can see and what it decides to do
    UI/          menus, HUD, minimap
    Editor/      the builders that generate the scenes
  Tests/         EditMode (pure rules) and PlayMode (real matches)
  Art/ Audio/ Prefabs/ Scenes/ Settings/
Assets/ThirdParty/Mirror
```

Two documents are worth reading before changing anything:

- **[docs/architecture.md](docs/architecture.md)** — the decisions and what they
  forced, including several traps this codebase sets repeatedly.
- **[docs/old-build-issues.md](docs/old-build-issues.md)** — the defects found in
  the original build, all of which are closed, and why each one happened.

Third-party assets and their licences are listed in
**[THIRD-PARTY.md](THIRD-PARTY.md)**.
