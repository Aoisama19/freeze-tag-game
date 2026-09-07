# Third-party assets and licences

Everything in this project that somebody else made, and the terms it is under.

## In the repository

| What | Where | Author | Licence |
|---|---|---|---|
| Mirror networking | `Assets/ThirdParty/Mirror` | Mirror Networking | MIT — see `Assets/ThirdParty/Mirror/LICENSE` |
| Mono.Cecil (bundled with Mirror) | `Assets/ThirdParty/Mirror/Plugins/Mono.Cecil` | Jb Evain | MIT — see the licence beside it |
| Y Bot character model | `Assets/_Project/Art/Characters/YBot` | Mixamo (Adobe) | Free for use in personal and commercial projects under Mixamo's terms |
| Humanoid idle, walk and run cycles | `Assets/_Project/Art/Characters/Animations` | Unity Technologies | Unity Companion License |
| Footstep and landing recordings | `Assets/_Project/Audio/Footsteps` | Unity Technologies (Starter Assets) | Unity Companion License — the licence text is in that folder |
| Orbitron typeface | `Assets/_Project/Art/UI/Fonts` | Matt McInerney | SIL Open Font License 1.1 |

Unity itself, the Universal Render Pipeline, the Input System, Cinemachine and
the AI Navigation package are Unity packages under Unity's own terms and are not
vendored here; they are restored from the package manifest.

## Made for this project

The sound effects in `Assets/_Project/Audio/Generated` are synthesised by
`SoundBuilder.cs` — a few hundred lines of arithmetic, belonging to nobody. The
map previews in `Assets/_Project/Art/UI/MapPreviews` are rendered from this
game's own scenes by `MapPreviewBuilder.cs`.

## Faizan Iqbal's own work

The three maps (3 Talwaar, Badshahi Masjid, the Faisalabad Clock Tower), their
textures and materials, and the menu artwork are the author's, carried across
from the original build of this game.

## Deliberately not used

Three things from the original project were left behind on licensing grounds.
Recording them here because the reasoning is otherwise invisible, and because
the older public repositories still contain some of them.

- **The music.** The old project's entire audio folder was a single hour-long
  MP3 of a commercial instrumental recording by a named classical musician. It
  is not a sound library and it is not ours to distribute.
- **The map thumbnails.** The old menu's map pictures are downloaded stock
  photographs; at least one still carries a stock agency's watermark across it.
  The previews here are rendered from the actual maps instead.
- **The minimap package.** The original used a paid Asset Store minimap. This
  build has its own, written from scratch, which also avoids redistributing
  someone else's commercial package in a repository.

## Licence for this project's own code and assets

**Not yet decided.** Everything above covers other people's work; the terms for
the author's own code, art and models are still an open question and should be
settled before this is shared with anyone.
