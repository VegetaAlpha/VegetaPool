# VegetaPool

Framework-agnostic object pooling for Unity. One `PoolSystem` core with no dependencies — use it
through a global static facade (`Pool.GetObj<T>()`) or construct/inject your own instance. Same
`Register` / `GetObj` / `ReleaseObj` / `DestroyPool` API either way.

Part of the [VegetaSystem](https://github.com/VegetaAlpha/VegetaSystem) family, but installable and
usable entirely on its own.

---

## Installation

Package Manager → **+** → **Install package from git URL…** → paste:

```
https://github.com/VegetaAlpha/VegetaPool.git?path=Pool
```

Package id: `com.vegetaalpha.pool`.

> There are no release tags yet, so this URL tracks the default branch and you get whatever is on
> `main` at install time. Once a tag exists, pin it — `...?path=Pool#v0.1.0` — so your project
> doesn't move under you.

### Requirements

| | |
|---|---|
| Unity | 2022.3 or newer |
| Dependencies | **none** |
| VContainer | only for the *VContainer adapter* sample |
| Addressables | only for the *Addressables loader* sample |

The core package references nothing — its `.asmdef` has an empty `references` array. Installing it
never drags a DI framework or Addressables into your project.

---

## Quick start

Make something poolable, then pick a usage mode.

```csharp
using UnityEngine;
using VegetaSystem;

public class Bullet : MonoBehaviour, IPoolable
{
    public void OnGet()     => gameObject.SetActive(true);
    public void OnRelease() => gameObject.SetActive(false);
}
```

**Static facade — no DI:**

```csharp
Pool.Register(bulletPrefab, initAmount: 20);

var bullet = Pool.GetObj<Bullet>();
bullet.transform.position = muzzle.position;

Pool.ReleaseObj(bullet);
```

**Constructed / injected instance:**

```csharp
var pool = new PoolSystem();          // or new PoolSystem(mySpawner)
pool.Register(bulletPrefab, 20);

var bullet = pool.GetObj<Bullet>();
pool.ReleaseObj(bullet);
```

---

## Making something poolable

Implement one of two interfaces on any `MonoBehaviour`. No base class, no attribute.

### `IPoolable` — one prefab, one pool

```csharp
public class Bullet : MonoBehaviour, IPoolable
{
    public void OnGet()     => gameObject.SetActive(true);
    public void OnRelease() => gameObject.SetActive(false);
}
```

### `ISubKeyPoolable` — several variants of the same type, each its own pool

Use this when one class covers interchangeable variants — enemy colors, bullet elements, card
suits. Each `GetSubKeyPool()` value gets a separate pool.

```csharp
public enum SphereType { Red, Blue, Yellow }

public class Sphere : MonoBehaviour, ISubKeyPoolable
{
    [SerializeField] private SphereType sphereType;   // set per prefab variant

    public string GetSubKeyPool() => sphereType.ToString();

    public void OnGet()     => gameObject.SetActive(true);
    public void OnRelease() => gameObject.SetActive(false);
}
```

```csharp
Pool.Register(redSpherePrefab,  10);   // subKey "Red"
Pool.Register(blueSpherePrefab, 10);   // subKey "Blue"

var red = Pool.GetObj<Sphere>(SphereType.Red.ToString());
```

`ISubKeyPoolable` deliberately does **not** extend `IPoolable`. That is what stops you calling
`GetObj<Sphere>()` without a subKey, or `GetObj<Bullet>("Red")` — the compiler rejects both.

**Pools are keyed by the prefab's actual runtime type**, `prefab.GetType().Name`, not the
compile-time `T`. A subclass registers under its own name even if `T` is inferred as its base.

---

## The two usage modes

Both modes run the exact same `PoolSystem` code. The only difference is who holds the instance.

### Static facade — `Pool`

`Pool` is a static class wrapping one lazily-created `PoolSystem`. Nothing to inject, nothing to
place in a scene.

```csharp
Pool.Register(prefab, 20);
var obj = Pool.GetObj<Bullet>();
```

Good for: prototypes, small projects, code that has no container to reach into.

### Constructed / injected — `PoolSystem`

```csharp
var pool = new PoolSystem(new DefaultSpawner());
```

With VContainer:

```csharp
public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<ISpawner, VContainerSpawner>(Lifetime.Singleton);
        builder.Register<PoolSystem>(Lifetime.Singleton).AsSelf();
    }
}
```

Good for: DI projects, tests, and any case where you want **more than one pool** — several
`PoolSystem` instances are allowed, e.g. one per child `LifetimeScope`.

`PoolSystem` has a single constructor with no `[Inject]` attribute, so DI frameworks pick it up
automatically while the class itself stays free of any DI reference.

### The two modes are mutually exclusive

Mixing them would silently give you two disconnected pools, so the package throws instead:

```csharp
Pool.GetObj<Bullet>();     // app is now in static mode
new PoolSystem();          // InvalidOperationException
```

```csharp
new PoolSystem();          // app is now in constructed mode
Pool.GetObj<Bullet>();     // InvalidOperationException
```

Enforced by a single `PoolUsageMode` field that never returns to `None` once claimed. Several
*constructed* pools remain fine — it is only mixing the two **modes** that throws.

> In the Editor this resets on domain reload, i.e. every time you press Play. If you disable
> **Reload Domain** in *Enter Play Mode Options*, the mode persists between Play sessions and the
> second run throws. Restart the Editor or re-enable domain reload.

### Reaching the instance from the static facade

```csharp
PoolSystem pool = Pool.GetPoolSystem();
```

For APIs that want a `PoolSystem` (like `SO_AllPoolData.ApplyTo`). It is a **method, not a
property**, on purpose: the first call creates the singleton, locks in static mode and can throw,
and none of that should fire because a debugger evaluated a property while you hovered over the
type.

---

## API reference

Everything on `Pool` forwards to the identical method on `PoolSystem`.

| Static | Instance | What it does |
|---|---|---|
| `Pool.Register<T>(prefab, initAmount)` | `pool.Register<T>(prefab, initAmount)` | Registers a prefab. Lazy — nothing is instantiated until first use |
| `Pool.GetObj<T>()` | `pool.GetObj<T>()` | Takes an `IPoolable` from the pool |
| `Pool.GetObj<T>(subKey)` | `pool.GetObj<T>(subKey)` | Takes an `ISubKeyPoolable` variant |
| `Pool.ReleaseObj(obj, ignoreParentPool, worldPosStay)` | `pool.ReleaseObj(...)` | Returns an object to its pool |
| `Pool.DestroyPool<T>()` | `pool.DestroyPool<T>()` | Destroys every subKey of `T` |
| `Pool.DestroyPool<T>(subKey)` | `pool.DestroyPool<T>(subKey)` | Destroys one variant |
| `Pool.DestroyAllPools()` | `pool.DestroyAllPools()` | Destroys everything this pool owns |
| `Pool.Spawner` | *(constructor argument)* | The `ISpawner` used to instantiate |
| `Pool.GetPoolSystem()` | *(you already hold it)* | The underlying instance |

**`Register` is lazy.** It only records prefab + amount. The `initAmount` instances are created the
first time that pool is actually touched by `GetObj`. Registering a hundred prefabs at startup
costs nothing but dictionary entries.

**`GetObj` returns `null`, it does not throw**, when nothing is registered for that type/subKey. It
logs `Pool config not found: <Type>/<subKey>` first.

### `ReleaseObj` parameters

| Parameter | Default | Effect |
|---|---|---|
| `ignoreParentPool` | `false` | `true` leaves the object where it is in the hierarchy instead of re-parenting it under the pool container |
| `worldPosStay` | `true` | Passed straight to `Transform.SetParent` — keep world position, or keep local |

### Callbacks on your poolable

| | Called when |
|---|---|
| `OnGet()` | The object is handed out — activate, reset state, restart timers |
| `OnRelease()` | The object comes back — deactivate, stop coroutines, clear references |

`OnRelease()` is where you clear references to other objects. A pooled object that keeps a
reference to something else pins it in memory for as long as the pool lives, which is forever
unless you say otherwise (see [Lifetime](#lifetime-and-cleanup-are-yours)).

---

## Releasing: two ways

Both end in the same code path. Pick per call site.

### 1. Through the pool — `pool.ReleaseObj(obj)`

```csharp
// static
Pool.ReleaseObj(bullet);

// instance
pool.ReleaseObj(bullet);
```

The pool finds the object's bookkeeping through a `Dictionary<int, …>` keyed by
`GetInstanceID()` — no component lookup at all. **Prefer this** wherever you already hold the pool
or are the code that spawned the object.

### 2. The object releases itself — `this.Release()`

`Release()` is an extension method on `IPoolableBase`, so a pooled object can return itself
without ever knowing about a `PoolSystem`:

```csharp
public class Bullet : MonoBehaviour, IPoolable
{
    private float life;

    private void Update()
    {
        life += Time.deltaTime;
        if (life > 3f)
            this.Release();        // no pool reference needed anywhere
    }

    public void OnGet()     { life = 0f; gameObject.SetActive(true); }
    public void OnRelease() => gameObject.SetActive(false);
}
```

From outside, the same extension reads as:

```csharp
bullet.Release();
```

**How it finds the right pool.** Every spawned instance carries an internal `PoolableTracker`
component recording which `PoolSystem` created it. `this.Release()` reads that — not a global
static — so it still routes correctly when several pools are alive at once, such as one per child
`LifetimeScope`.

**The trade-off.** It costs one `GetComponent<PoolableTracker>()` per call, which
`pool.ReleaseObj(obj)` skips entirely.

| | `pool.ReleaseObj(obj)` | `this.Release()` |
|---|---|---|
| Needs a pool reference | yes | no |
| Component lookup | none | one `GetComponent` |
| Best for | spawners, controllers, managers | self-expiring objects, effects, projectiles |

**Releasing twice is safe** — the second call logs `Object with name X is already release` and does
nothing. Releasing an object that never came from a pool logs a warning and does nothing.

---

## Supplying prefabs

`Register()` takes a `Component`. Where that component came from is entirely your business — the
pool has no opinion and no loader of its own. Two supported routes follow.

### Route A — `SO_AllPoolData` (built in, direct references)

Create the assets: **Create → Pool → PoolData** for each entry, then one **Create → Pool →
AllPoolData** collecting them.

`SO_PoolData` has two modes, and the Inspector only shows the fields belonging to the one you pick:

| Mode | Fields | Use for |
|---|---|---|
| `Single` | `Prefab`, `InitAmount` | one `IPoolable` prefab |
| `Multiple` | `PoolItems[]` of prefab + amount | several `ISubKeyPoolable` variants |

The Inspector rejects a prefab that doesn't match the mode — dropping an `ISubKeyPoolable` into
`Single` clears the field and logs why.

Then apply it once at startup:

```csharp
public class PoolBootstrap : MonoBehaviour
{
    [SerializeField] private SO_AllPoolData poolConfig;

    // Static mode
    private void Awake() => poolConfig.ApplyTo(Pool.GetPoolSystem());
}
```

```csharp
// DI mode
[Inject]
public void Construct(PoolSystem pool) => poolConfig.ApplyTo(pool);
```

`ApplyTo` walks the list and calls `Register()` for every entry. That is all it does — the pool
never learns that `SO_AllPoolData` exists.

### Route B — Addressables

**`SO_AllPoolData` and Addressables do not mix, and this is the part worth understanding.**

`SO_PoolData.Prefab` is a plain `MonoBehaviour` field — a **direct, hard reference**. The moment
the `SO_AllPoolData` asset is loaded, Unity loads every prefab it points at, plus every mesh,
material, texture and audio clip those prefabs pull in. That is exactly what Addressables exists to
prevent. Putting Addressable prefabs into `SO_PoolData` silently defeats the whole system: they get
pulled into the build as direct dependencies and load up front anyway.

**So with Addressables you do not use `SO_PoolData` at all.** You resolve the prefab by key at
runtime, then call `Register()` the normal synchronous way.

Only *getting the prefab reference* is async. Everything after that — registering, prewarming,
`GetObj`, `ReleaseObj` — is unchanged and fully synchronous. There is **no Addressables-specific
`ISpawner`**, because pooled instances are created with plain `Instantiate` from a prefab you
already hold.

The shipped sample is one method:

```csharp
public static class AddressablePoolLoader
{
    public static async Task RegisterAsync<T>(
        this PoolSystem pool, string addressableKey, int initAmount) where T : Component
    {
        var handle = Addressables.LoadAssetAsync<GameObject>(addressableKey);
        var prefab = await handle.Task;
        pool.Register(prefab.GetComponent<T>(), initAmount);
    }
}
```

Usage in both modes:

```csharp
// static
await Pool.GetPoolSystem().RegisterAsync<Bullet>("bullet_basic", 20);

// DI
await pool.RegisterAsync<Bullet>("bullet_basic", 20);
```

```csharp
private async void Start()
{
    var pool = Pool.GetPoolSystem();

    await pool.RegisterAsync<Bullet>("bullet_basic", 20);
    await pool.RegisterAsync<Enemy>("enemy_grunt", 10);

    // Only now is it safe to spawn.
    var bullet = Pool.GetObj<Bullet>();
}
```

#### Three things to get right

**1. Await registration before the first `GetObj`.** Registration is async; `GetObj` is not. Call
`GetObj<Bullet>()` before the await completes and you get a `Pool config not found: Bullet`
warning and a `null`.

**2. Do not release the Addressables handle while the pool is alive.** `Register()` stores the
prefab reference and the pool instantiates from it *later*, every time it needs to grow. Calling
`Addressables.Release(handle)` right after registering unloads the prefab out from under the pool.
The shipped sample never releases — deliberately, but it also means nothing ever unloads it.

**3. Pair handle release with `DestroyPool`.** Since the sample drops the handle on the floor,
track it yourself if you care about unloading. A small wrapper:

```csharp
public class AddressablePoolRegistry
{
    private readonly PoolSystem pool;
    private readonly Dictionary<Type, AsyncOperationHandle<GameObject>> handles = new();

    public AddressablePoolRegistry(PoolSystem pool) => this.pool = pool;

    public async Task RegisterAsync<T>(string key, int initAmount) where T : Component
    {
        var handle = Addressables.LoadAssetAsync<GameObject>(key);
        var prefab = await handle.Task;

        pool.Register(prefab.GetComponent<T>(), initAmount);
        handles[typeof(T)] = handle;          // keep it alive, and findable
    }

    /// <summary>Destroy the pool first, then unload the asset it was instantiating from.</summary>
    public void DestroyAndUnload<T>() where T : class, IPoolable
    {
        pool.DestroyPool<T>();

        if (handles.Remove(typeof(T), out var handle))
            Addressables.Release(handle);
    }
}
```

Order matters: destroy the pool **before** releasing the handle, never the other way round.

### Route C — anything else

There is no route C, and that is the point. Anything that can hand you a `Component` works the
same way: `Resources.Load`, an addressable label batch, a prefab list on a `MonoBehaviour`, a
factory. Get the reference however you like, then call `Register()`.

---

## Lifetime and cleanup are yours

**This pool never shrinks and never cleans itself up.** That is a deliberate design decision, and
it is the single thing most likely to bite you. Read this section.

### What the pool does on its own

| Event | What happens |
|---|---|
| `Register(prefab, 20)` | Nothing yet — lazy |
| First `GetObj<T>()` | Container GameObject created, 20 instances prewarmed |
| `GetObj<T>()` with an empty pool | **Creates one more instance.** The pool grows |
| `ReleaseObj(obj)` | Object deactivated and parked. **Still in memory** |
| Scene unload | **Nothing.** The pool root is `DontDestroyOnLoad` |
| DI scope disposed | **Nothing.** `PoolSystem` is not `IDisposable` |
| Application quit | Unity tears everything down |

The pool grows to your **peak concurrent usage** and stays there for the rest of the session. If a
boss fight spawns 300 projectiles once, those 300 instances live in memory until you say otherwise
— even in the main menu, three scenes later.

### Why `DontDestroyOnLoad`

A scene unloading is not a statement that you are finished with the pool. Treating it as one would
also break things outright: the pool's internal entries would keep pointing at destroyed container
transforms, and the next `GetObj` would throw `MissingReferenceException`.

So the pool survives scene loads on purpose, and the next scene reuses it already warmed up. The
flip side is that **only you can end its life**.

### Your side of the deal

```csharp
Pool.DestroyPool<Bullet>();              // one type, every subKey
Pool.DestroyPool<Sphere>("Red");         // one variant
Pool.DestroyAllPools();                  // everything
```

`DestroyPool` destroys both the parked instances *and* the ones currently checked out — including
any you re-parented elsewhere in the scene — then removes the container.

A reasonable discipline:

- **`DestroyPool<T>()`** when a feature is done with a type: leaving a level, closing a mode,
  finishing a boss fight.
- **`DestroyAllPools()`** when switching between major sections of the game, or at teardown.
- **Nothing at all** for genuinely global objects — UI popups, hit effects, floating damage
  numbers. Those *should* live for the session; that is the point of a pool.

### DI users: destroy with your scope

Nothing calls cleanup for you, and each `PoolSystem` instance owns its own `DontDestroyOnLoad`
root. Recreate a `LifetimeScope` without cleaning up and you strand an unreachable `[PoolSystem]`
root every time:

```csharp
public class GameLifetimeScope : LifetimeScope
{
    protected override void OnDestroy()
    {
        // Resolve before base.OnDestroy(), which disposes the container.
        if (Container != null && Container.TryResolve<PoolSystem>(out var pool))
            pool.DestroyAllPools();

        base.OnDestroy();
    }
}
```

### Symptoms of poor management

| Symptom | Cause |
|---|---|
| Memory creeps up every level | Pools grew to peak usage and were never destroyed |
| Hundreds of inactive clones under `[PoolSystem]` | Normal for a live pool — a leak only if that type is done being used |
| Several `[PoolSystem]` roots in DontDestroyOnLoad | A DI scope was recreated without `DestroyAllPools()` |
| Assets never unload despite Addressables | A pooled prefab still holds the handle, or `OnRelease` doesn't clear references |
| `MissingReferenceException` from `GetObj` | Something destroyed the pool's containers behind its back |

**Release is not destroy.** `ReleaseObj` returns an object for reuse; the memory stays. Only
`DestroyPool` / `DestroyAllPools` gives it back.

---

## Samples

Package Manager → **VegetaPool** → **Samples** tab → **Import**.

Each sample is independent — import only what you need. **Both have a prerequisite that must be
installed first.**

### VContainer adapter

An `ISpawner` backed by `IObjectResolver.Instantiate`, so pooled instances get their own
`[Inject]` dependencies resolved instead of a plain `Instantiate`.

1. **Install VContainer first** — Package Manager → git URL:
   ```
   https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer
   ```
2. *Then* import the sample.
3. Register it:
   ```csharp
   builder.Register<ISpawner, VContainerSpawner>(Lifetime.Singleton);
   builder.Register<PoolSystem>(Lifetime.Singleton).AsSelf();
   ```

### Addressables loader

`AddressablePoolLoader.RegisterAsync<T>()` — resolve a prefab by Addressable key, then `Register()`
it normally. A starting point to adapt, not a full config system.

1. **Install Addressables first** — Package Manager → Unity Registry → *Addressables*.
2. *Then* import the sample.

> **Order matters.** Each sample ships an `.asmdef` referencing its dependency by name
> (`VContainer`, `Unity.Addressables`). Import the sample into a project that lacks that package
> and the reference cannot resolve, producing compile errors across the assembly. Install the
> dependency first and the sample compiles immediately.
>
> To recover: either install the missing package, or delete the imported sample folder under
> `Assets/Samples/VegetaPool/`.

### Demo scenes

The repository also carries two runnable demo scenes under `Assets/PoolSamples/` — not shipped in
the package, but clone the repo to see both modes side by side:

| | |
|---|---|
| `StaticPool/` | Buttons spawning cubes and spheres through `Pool.*` |
| `VContainerPool/` | The same demo, injected through a `LifetimeScope` |

The two controllers are line-for-line identical apart from `Pool.` versus `pool.`. Open them
**one at a time** — running both in one Play session trips the mutual-exclusion guard.

---

## Implementation walkthrough

Start to finish, static mode:

1. **Install** the package (URL above).
2. **Implement `IPoolable`** (or `ISubKeyPoolable`) on the MonoBehaviour you want pooled. Put
   activation in `OnGet`, deactivation and state reset in `OnRelease`.
3. **Make the prefab**, with that component on the root.
4. **Create the config**: Create → Pool → PoolData for each entry, then Create → Pool → AllPoolData
   collecting them. *(Skip if you are using Addressables — see Route B.)*
5. **Bootstrap once**, early:
   ```csharp
   [SerializeField] private SO_AllPoolData poolConfig;
   private void Awake() => poolConfig.ApplyTo(Pool.GetPoolSystem());
   ```
6. **Spawn**: `var bullet = Pool.GetObj<Bullet>();`
7. **Release**: `Pool.ReleaseObj(bullet)` from the spawner, or `this.Release()` from inside the
   object.
8. **Destroy** when the feature is done: `Pool.DestroyPool<Bullet>()`.

For DI mode, replace steps 5–8 with an injected `PoolSystem` and add `DestroyAllPools()` to your
scope's `OnDestroy`.

### Custom instantiation

`ISpawner` is the one thing the pool knows about creating objects:

```csharp
public interface ISpawner
{
    T Spawn<T>(T prefab) where T : Component;
}
```

Default is `DefaultSpawner` (plain `Object.Instantiate`). Swap it before first use:

```csharp
Pool.Spawner = new MySpawner();          // static — must be set before the first pool call
var pool = new PoolSystem(new MySpawner());   // DI
```

---

## Design notes

- **Zero dependencies in the core.** `PoolSystem` takes an `ISpawner` and nothing else. The
  VContainer and Addressables adapters ship as optional Samples specifically so installing this
  package never forces those packages on a project that doesn't want them.
- **Static facade and constructed instances are mutually exclusive**, enforced by one
  `PoolUsageMode` field that never returns to `None` once claimed. Using both throws instead of
  silently producing two disconnected pools. Several *constructed* pools are still fine.
- **`this.Release()` needs no `PoolSystem` reference** because each spawned object carries an
  internal `PoolableTracker` recording which instance spawned it — not a global lookup, so it
  resolves correctly with several pools alive.
- **Hot-path bookkeeping is keyed by `GetInstanceID()`** in a dictionary, never `GetComponent`.
  That is why `PoolableTracker` exists rather than storing state on your script.
- **The pool root is `DontDestroyOnLoad` in both modes**, and cleanup is always an explicit call.
  `PoolSystem` is deliberately not `IDisposable`: cleanup is something you say, not something that
  happens to you.

---

## About

Author: [VegetaAlpha](https://github.com/VegetaAlpha) · Package `com.vegetaalpha.pool`

See also [VegetaSystem](https://github.com/VegetaAlpha/VegetaSystem) — the larger framework this
pool is part of, adding a layered UI manager, scene loading helpers and generic singleton bases.
