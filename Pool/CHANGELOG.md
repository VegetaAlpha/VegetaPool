# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-01

First public release.

### Added

- `PoolSystem` — the pool core. `Register` / `GetObj` / `ReleaseObj` / `DestroyPool` /
  `DestroyAllPools`, over any `ISpawner`. No dependencies on any DI framework.
- `Pool` — static facade forwarding to a single `PoolSystem`, for projects that don't inject.
  The two modes are mutually exclusive and enforced at runtime.
- `IPoolable` and `ISubKeyPoolable` — poolable on any `MonoBehaviour`, with `ISubKeyPoolable`
  giving one pool per variant via a string key.
- `this.Release()` extension — a pooled object can return itself without holding a pool reference,
  routed through an internal tracker so it still finds the right pool with several alive.
- `Unregister<T>()` — drops what `Register` stored. Rarely needed, since `DestroyPool` keeps the
  config on purpose so a pool rebuilds itself. Use it when the prefab is about to stop being
  valid, such as before releasing an Addressables handle.
- `ISpawner` / `DefaultSpawner` — the single seam for instantiation, so DI containers can resolve
  `[Inject]` members on pooled instances.
- `SO_AllPoolData` / `SO_PoolData` — ScriptableObject prefab config with a custom Inspector that
  draws only the fields for the selected mode and rejects prefabs that don't match it.
- Samples: *Static Pool Demo* and *VContainer Pool Demo* — the same runnable scene built both
  ways, using `UnityEngine.UI.Text` on the built-in font so they run as imported with no
  extra setup.

### Notes

- The pool root is `DontDestroyOnLoad` and the pool never shrinks. Destroying pooled objects is
  always an explicit `DestroyPool` / `DestroyAllPools` call; a scene unload is not one.
