# Baraf-Paani

A multiplayer freeze tag game set in three Pakistani landmarks. One player
catches, everyone else runs. A frozen runner stays frozen until a team-mate
reaches them.

Play alone against bots, or host a match and let friends on the same network find
it in a list. Built in Unity 6.3 LTS using Mirror for networking.

![The main menu](docs/screenshots/menu.png)

## The game

A round runs for as long as you choose, from one minute to ten.

The **catcher** freezes runners by touching them. They always know where every
runner is. On a 120 metre map, a catcher who has to hunt by sight plays as broken
rather than fair.

**Runners** stay alive by not being touched, and free frozen team-mates by
reaching them. They can see their team-mates at all times, but they only see the
catcher while the catcher is in view. Breaking line of sight is worth doing.

The catcher wins by freezing everybody. The runners win if anyone is still free
when the clock runs out.

Nobody can be frozen for the first few seconds of a round, so a catcher standing
near a spawn point cannot take people before they have had a chance to move. The
HUD shows how long you are still safe for.

Scores carry across rounds within a match.

### Power-ups

Six pickups sit around the middle of each arena. They are closer in than the
spawn ring, so going for one means heading towards trouble. You can carry three
at a time.

* **Speed.** Considerably faster for a few seconds, whether you are running away
  or running somebody down.
* **Invisible.** Off everyone's screen, off the minimap, and out of the catcher's
  otherwise unlimited view of the runners.
* **Clone.** Leaves a standing copy of you wearing your role, so the catcher has
  to guess. Tagging it appears to work.

Bots use theirs too, but only when it counts: mid chase, or mid flight.

## Controls

| Action | Keys |
|---|---|
| Move | `W` `A` `S` `D` or arrow keys, left stick |
| Look | Mouse, right stick |
| Sprint | `Left Shift`, left stick press |
| Jump | `Space` |
| Use the selected power-up | Left mouse, `Enter` |
| Cycle power-ups | `1` and `2`, D-pad left and right |
| Switch side in the lobby | `E` |
| Start the match (host, in the lobby) | Left mouse, `Enter` |
| Menu: resume, settings, leave, quit | `Escape`, gamepad Start |

Volume and look sensitivity live under Settings, which you can reach from the
main menu and from the in-game menu. Both are remembered between sessions.

## The maps

There are three, chosen from the menu. Each one shows a picture rendered from the
map itself.

**3 Talwaar**, a Lahore roundabout and the streets around it.

![3 Talwaar](docs/screenshots/3talwaar.png)

**Badshahi Masjid**, the mosque's courtyard, water channels and gate.

![Badshahi Masjid](docs/screenshots/badshahimasjid.png)

**Faisalabad Clock Tower**, with the eight bazaars radiating out from it.

![Faisalabad Clock Tower](docs/screenshots/clocktower.png)

Each map is a 120 metre square cut out of a much larger model. The playable
square, the spawn ring, the power-up ring and the minimap framing are per-map
numbers in a single table in `PlayableSceneBuilder.cs`.

## Playing together

**Hosting.** Pick a side, a map and a round length, then choose *Host a match*.
The match waits in a lobby until you start it. Anyone who joins picks their own
side with `E`. There can only be one catcher, and a bot will never hold that seat
against a person.

**Joining.** *Join* opens a screen listing matches being hosted on your network.
Click one and you go straight into whatever map the host chose.

If the list stays empty, type an address instead. That always works and broadcast
does not: it cannot cross subnets, cannot reach the internet, and is blocked
outright on a good many university and office networks. A firewall prompt
dismissed on the host's machine will also leave it invisible.

**Bots** fill any empty runner slots and step aside as people arrive. You can
switch them off, in which case the players present have to cover both sides. A
match with nobody catching refuses to start rather than running a round that
cannot be won.

## Running it from source

You will need **Unity 6000.3.23f1** and **Git LFS**. The models and audio are
stored through LFS, so cloning without it gets you pointer files instead of
assets.

```
git lfs install
git clone <this repository>
```

Open the folder in Unity Hub, open `Assets/_Project/Scenes/MainMenu.unity`, and
press Play.

Any map scene also runs on its own without the menu. Open one, press Play, and it
starts a single player match. That is deliberate, and the tests rely on it.

### The scenes are generated

The menus and map scenes are built by editor scripts rather than assembled by
hand, so how they are put together is readable and reviewable instead of being
inspector state that nobody can diff. Under the **Baraf-Paani** menu in the
editor:

| Command | What it does |
|---|---|
| Rebuild Playable Scene | The character prefabs and all three map scenes |
| Rebuild Main Menu | The menu |
| Rebuild Connecting Scene | The join screen |
| Rebuild Character Animator | The locomotion blend tree, from the clips |
| Rebuild Sounds | Synthesises the sound effects |
| Rebuild Map Previews | Renders the map pictures the menu shows |
| Probe Maps | Reports each map's bounds, for setting arena numbers |

Running one of these replaces its output from scratch. You can edit those scenes
by hand, but the next rebuild will overwrite your changes.

## Tests

102 EditMode tests and 74 PlayMode tests.

```
Unity.exe -runTests -batchmode -nographics -projectPath . -testPlatform EditMode
Unity.exe -runTests -batchmode -projectPath . -testPlatform PlayMode
```

**PlayMode must run without `-nographics`.** The minimap renders to a texture,
and without a graphics device that fails and takes most of the suite down with
it.

The EditMode tests cover the rules as pure functions: who may freeze whom, what
the lobby allows, how a round resolves. The PlayMode tests stand up real matches.
Every map gets loaded, hosted and played, to check that it has a camera, a baked
NavMesh, spawn points that are not on rooftops, and bots that are actually on the
mesh.

Screenshots come from a play mode tool that is skipped unless you ask for it:

```
Unity.exe -runTests -batchmode -projectPath . -testPlatform PlayMode -captureScreenshots
```

## Building and sharing it

```
Unity.exe -quit -batchmode -nographics -projectPath . -buildWindows64Player Builds/BarafPaani.exe
```

A Windows build is not a single file. `BarafPaani.exe` needs `BarafPaani_Data/`,
`UnityPlayer.dll` and `MonoBleedingEdge/` sitting beside it, so anything you share
has to be a zip of the whole `Builds` folder. The bare .exe will not start.

Builds are not committed. They run to tens of megabytes, change wholesale every
time, and git keeps every version for ever, so `Builds/` is in `.gitignore`.
Attach the zip to a GitHub release instead, or host it somewhere like itch.io.

Windows SmartScreen will warn about an unsigned executable downloaded from the
internet. That is expected for an unsigned build rather than a sign of anything
wrong, but it is worth warning people before they run into it.

## How it is put together

```
Assets/_Project/
  Scripts/
    Core/        Starting and joining matches, and what the menu chose
    Gameplay/    Freezing, roles, rounds, power-ups. The rules.
    AI/          What a bot can see, and what it decides to do
    UI/          Menus, HUD, minimap
    Editor/      The builders that generate the scenes
  Tests/         EditMode for pure rules, PlayMode for real matches
  Art/ Audio/ Prefabs/ Scenes/ Settings/
Assets/ThirdParty/Mirror
```

[docs/architecture.md](docs/architecture.md) covers the design decisions and
what each one forced, including several traps this codebase sets repeatedly.

Third-party assets and their licences are in
[THIRD-PARTY.md](THIRD-PARTY.md).

## Licence

Copyright (c) 2026 Faizan Iqbal. All rights reserved. See [LICENSE](LICENSE).
