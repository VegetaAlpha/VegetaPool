# VegetaPool

Framework-agnostic Unity object pooling. One `PoolSystem` core, no DI framework required — use it as a global static-style facade (`Pool.GetObj<T>()`) or construct/inject your own `PoolSystem` instance. Same `Register`/`GetObj`/`ReleaseObj`/`DestroyPool` API either way.

## Install

```
https://github.com/VegetaAlpha/VegetaPool.git?path=Pool#<tag>
```

Package id: `com.vegetaalpha.pool`.

## Quick start

**No DI — static-style facade:**

```csharp
Pool.Register(bulletPrefab, initAmount: 20);
var bullet = Pool.GetObj<Bullet>();
bullet.Release(); // or Pool.ReleaseObj(bullet)
```

**Constructed / injected instance** (VContainer, Zenject, plain `new`, whatever):

```csharp
var pool = new PoolSystem(new DefaultSpawner()); // or any ISpawner
pool.Register(bulletPrefab, 20);
var bullet = pool.GetObj<Bullet>();
bullet.Release(); // still works without holding `pool` — see "How self-release works" below
```

Don't mix the two styles in the same app — the static `Pool` facade and manually constructing `PoolSystem` are mutually exclusive; using both throws `InvalidOperationException` (see [[Design notes]]).

## Making something poolable

Implement `IPoolable` (single pool) or `ISubKeyPoolable` (several interchangeable variants, each keyed by `GetSubKeyPool()`, e.g. different colors of the same enemy) on any `MonoBehaviour` — no base class required:

```csharp
public class Bullet : MonoBehaviour, IPoolable
{
    public void OnGet() => gameObject.SetActive(true);
    public void OnRelease() => gameObject.SetActive(false);
}
```

`Register<T>()` keys by the prefab's actual runtime type (`prefab.GetType()`), not the compile-time `T` — a subclass registers under its own name even if `T` is inferred as its base class.

## Supplying prefabs

`PoolSystem`/`Pool` never look at where a prefab comes from — `Register()` just takes a `Component`. Something else has to call `Register()`. Built in: `SO_AllPoolData` (a `ScriptableObject` list of prefab + init-amount entries, supports both single and multi-variant configs) — call `soAllPoolData.ApplyTo(pool)` once at startup and it registers everything. Works identically whether `pool` came from your own DI container or from `Pool.GetPoolSystem()` (the static facade's escape hatch, for the APIs that want the instance itself).

Anything else that can hand you a `Component` reference works the same way — load it however you like, then call `Register()`. See the samples below for two examples (VContainer spawning, Addressables loading).

## Samples

Import from Package Manager → this package → Samples tab. Each is independent — only import the one(s) you need.

- **VContainer adapter** — `ISpawner` backed by `IObjectResolver.Instantiate`, so spawned instances get their own `[Inject]` dependencies resolved. Requires `jp.hadashikick.vcontainer` installed. Register in your `LifetimeScope`:
  ```csharp
  builder.Register<ISpawner, VContainerSpawner>(Lifetime.Singleton);
  builder.Register<PoolSystem>(Lifetime.Singleton).AsSelf();
  ```
- **Addressables loader** — minimal example (`AddressablePoolLoader.RegisterAsync<T>()`) resolving a prefab by Addressable key, then calling `Register()` the normal synchronous way. Requires `com.unity.addressables` installed. This is a starting point to adapt, not a full config system like `SO_AllPoolData` — Addressables only replaces *how you get the prefab reference*; spawning pooled instances afterward is still plain `ISpawner`, no Addressables-specific spawn path needed.

## Design notes

- **Core has zero dependencies on any DI framework or config source.** `PoolSystem` takes an `ISpawner` (one method: `T Spawn<T>(T prefab)`) and nothing else. `VContainerSpawner`/`AddressablePoolLoader` are optional adapters, shipped as Samples (excluded from compilation until imported) specifically so installing the package never forces `jp.hadashikick.vcontainer` or `com.unity.addressables` on a project that doesn't want them.
- **`Pool` (static-style facade) vs constructing `PoolSystem` yourself are mutually exclusive**, enforced at runtime by a single `PoolUsageMode` field on `PoolSystem` that never returns to `None` once claimed — using both in the same app throws instead of silently producing two disconnected pools. The constructor always claims `Constructed`; `Pool` corrects it to `StaticFacade` right after, which is how the facade builds its own singleton without tripping the guard against manual construction. Several manually-constructed pools are still fine (`Constructed` → `Constructed`); it's only mixing the two *modes* that throws.
- **The pool root is `DontDestroyOnLoad`, in both usage modes.** Once created it lives until someone explicitly calls `DestroyPool`/`DestroyAllPools` — a scene unload is not a statement that you're done with the pool, and letting one destroy the root leaves `PoolEntry.parent` dangling so the next `GetObj` throws `MissingReferenceException`. This is the same contract as `PoolSystem` deliberately not being `IDisposable`: cleanup is something you say, not something that happens to you. Pooled objects you reparent out and let a scene destroy are still accounted for — `PoolableTracker.OnDestroy` drops them from the pool's bookkeeping.
- **`this.Release()` works without holding a `PoolSystem` reference** because each spawned object gets an internal `PoolableTracker` component recording which `PoolSystem` instance spawned it (`OwnerPool`) — not a static/global lookup, so it still resolves correctly even with multiple `PoolSystem` instances alive (e.g. one per VContainer child `LifetimeScope`). Prefer `pool.ReleaseObj(obj)` directly wherever you already hold `pool` — it skips a `GetComponent` call that `this.Release()` needs to find the tracker.
- **Hot-path bookkeeping** (`Get()`/`Release()`) is keyed by `GetInstanceID()` in a `Dictionary`, never `GetComponent()` — this is why `PoolableTracker` exists instead of storing state on the poolable script itself.
- This package was split into two separate packages (`Pool` + `Pool.VContainer`, later renamed `Pool.Injection`) for a while, each with its own copy of the core, specifically so installing one could never include the other's files at all (not even uncompiled). That turned out to be more sync overhead than it was worth for how small the actual DI-specific surface is (one file, `VContainerSpawner.cs`) — merged back into this single package with Samples for the optional parts, which gets the same "never forces the dependency on you" property without the duplication.
