# Baraf-Paani

A 3D freeze-tag game in Unity and C#, with single-player and online multiplayer
running on one shared codebase.

*Baraf-Paani* is the South Asian playground version of freeze tag: a catcher
freezes runners on touch, and runners free frozen teammates by reaching them
first.

## Status

Rebuild in progress. This replaces two earlier repositories that had drifted
into separate, partly-broken copies of the same game — see
[docs/old-build-issues.md](docs/old-build-issues.md) for what went wrong and
[docs/architecture.md](docs/architecture.md) for the approach this time.

## Stack

- Unity 6.3 LTS
- C#
- Mirror — networking, used for both game modes

## Layout

```
Assets/
  _Project/        our code and content
  ThirdParty/      imported assets, kept separate
docs/              design notes and decisions
```
