# Issues found in the previous build

Read out of `Freeze_Tag_Single` and `Freeze_Tag_Multiplayer` before starting the
rebuild. Line references are against those repositories at the commits before
the READMEs were added.

Recording these for two reasons: so none of them get faithfully reproduced, and
so the rebuild has a checklist of things that must demonstrably work.

---

## 1. The catcher AI can hang the game

`Freeze_Tag_Single/Assets/_C#/AI/CatcherAI.cs:184-201`

```csharp
private IEnumerator Delay()
{
    _flag = true;
    while (true)
    {
        if (!_target) { _target = GetTargetToFollow(); yield return new WaitForSeconds(5f); }
        else          { _flag = false; StopCoroutine(Delay()); }   // <-- no yield
    }
}
```

`StopCoroutine(Delay())` calls `Delay()` again, which constructs a *new* iterator
object. `StopCoroutine` matches on reference, so it stops nothing. Control falls
back to `while (true)`, `_target` is still non-null, and the `else` branch runs
again — with no `yield` on that path. The loop spins forever inside a single
frame and the editor locks up.

Triggered whenever the catcher reacquires a target after having lost all of
them, which is common. Likely responsible for a good share of the freezes.

**Rebuild:** no `while (true)` without a `yield` on every path. Prefer a plain
state machine ticked from `Update` over long-lived coroutines holding state.

---

## 2. Field-of-view returns the last result, not the best one

`Freeze_Tag_Single/Assets/_C#/AI/FieldOfView.cs:89-96`

`canSeePlayer` is assigned *inside* the loop over candidates. With more than one
target in range, the final value reflects whichever collider happened to be
processed last, not whether anything is visible. `OverlapSphere` ordering is not
guaranteed, so sight flickers unpredictably.

**Rebuild:** compute into a local, `break` on first confirmed sighting, assign
once after the loop.

---

## 3. The Clone power-up is shared between every character

`Freeze_Tag_Single/Assets/_C#/Powerups/Clone.cs:28,51`

```csharp
_clones = GameObject.FindGameObjectsWithTag("Clone");
```

This is a scene-wide lookup, so every character holding the power-up finds and
manipulates the same clone objects. Two users at once fight over them. It also
indexes `_clones[0]` with no length check, so it throws if nothing in the scene
carries the tag.

**Rebuild:** clones are spawned by, and owned by, the character that used the
power-up. Over the network they are server-spawned objects, not scene lookups.

---

## 4. Two competing multiplayer implementations

`Freeze_Tag_Multiplayer/Assets/zzzzz multiplayer/Scripts/`

`PlayerController.cs:32` and `PlayerFreezeManager.cs:84` both bind **F** and both
implement freeze/unfreeze against different state — `PlayerController` has its
own `isCatcher`/`isFrozen` pair, `PlayerFreezeManager` has a `playerRole` enum
and its own `isFrozen`. If both components sit on the prefab they contradict
each other.

**Rebuild:** one component owns freeze state. The earlier attempt gets deleted
rather than left in place.

---

## 5. Freeze state stored as a physics layer

Both repositories, throughout.

Frozen characters were marked by assigning `gameObject.layer = "Freeze"`, then
disabling `Animator`, `NavMeshAgent`, `ThirdPersonCharacter`, `FieldOfView` and
input individually at each call site. That has three consequences:

- It does not replicate — a layer change is local, which is the root reason the
  multiplayer version needed a parallel implementation.
- Game state and physics/rendering configuration are the same variable.
- The disable list is repeated at four call sites and they do not agree with each
  other, so a character frozen by the AI ends up in a different state than one
  frozen by a player.

**Rebuild:** `[SyncVar] bool isFrozen` with a hook that applies all the side
effects in exactly one place.

---

## 6. The two repos silently diverged

`CatcherAI.cs` and `Attack.cs` in the single-player repo guard against
re-freezing an already-frozen target:

```csharp
if (distance <= catchDistance && _target.gameObject.layer != LayerMask.NameToLayer("Freeze"))
```

The multiplayer copy has no such guard. Without it `Actions.Baraf` fires more
than once per victim and decrements the runners-left counter too far — the
counter that decides win/lose.

`RunnerAI.UpdateBehavior` was also rewritten in the single-player repo and left
alone in the multiplayer one, leaving `UnfreezeRunner()` as dead code in one and
live in the other.

**Rebuild:** one codebase. This class of bug stops being possible.

---

## 7. Smaller things worth not repeating

- `GameManager.cs:133` — `player.transform.GetChild(2)` addresses a child by
  index. Silently grabs the wrong object the moment the prefab hierarchy changes.
- `GameManager.Update()` rebuilds a string and walks the runner list every frame
  to display the frozen count. Should update when the count changes.
- `GameManager` uses a singleton plus a static `Actions` event class, both of
  which assume a single game in a single process.
- `FieldOfView` hard-codes radii and angles in `Start()` after they are exposed
  as public inspector fields, so the inspector values are misleading.
