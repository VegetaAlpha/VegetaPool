# VegetaPool

Framework-agnostic object pooling for Unity. No dependencies. Use it through a static facade
(`Pool.GetObj<T>()`) or construct/inject your own `PoolSystem` — same API either way.

Part of [VegetaSystem](https://github.com/VegetaAlpha/VegetaSystem), usable entirely on its own.

## Install

Package Manager → **+** → **Install package from git URL…**

```
https://github.com/VegetaAlpha/VegetaPool.git?path=Pool
```

| | |
|---|---|
| Unity | 2022.3+ |
| Dependencies | none |
| VContainer | only for the *VContainer Pool Demo* sample |

No tags yet, so this URL tracks `main`. Once one exists, pin it: `...?path=Pool#v0.1.0`.

## Quick start

```csharp
using UnityEngine;
using VegetaSystem;

public class Bullet : MonoBehaviour, IPoolable
{
    public void OnGet()     => gameObject.SetActive(true);
    public void OnRelease() => gameObject.SetActive(false);
}
```

```csharp
Pool.Register(bulletPrefab, initAmount: 20);

var bullet = Pool.GetObj<Bullet>();
bullet.transform.position = muzzle.position;

Pool.ReleaseObj(bullet);
```

---

## Poolable objects

Implement one of two interfaces on any `MonoBehaviour`. No base class, no attribute.

| | |
|---|---|
| `OnGet()` | Handed out — activate, reset state |
| `OnRelease()` | Returned — deactivate, stop coroutines, **clear references** |

Clearing references in `OnRelease` matters: a pooled object holding a reference pins that object in
memory for as long as the pool lives, which is until you destroy it.

### `IPoolable` — one prefab, one pool

Shown above.

### `ISubKeyPoolable` — several variants, each its own pool

For interchangeable variants of one class: enemy colours, bullet elements, card suits.

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
Pool.Register(redSpherePrefab,  10);      // subKey "Red"
Pool.Register(blueSpherePrefab, 10);      // subKey "Blue"

var red = Pool.GetObj<Sphere>(SphereType.Red.ToString());
```

`ISubKeyPoolable` does not extend `IPoolable`, so the compiler rejects `GetObj<Sphere>()` without a
subKey and `GetObj<Bullet>("Red")` with one.

Pools are keyed by `prefab.GetType().Name` — the **runtime** type, not the compile-time `T`. A
subclass registers under its own name even if `T` is inferred as its base.

---

## Two usage modes

Same `PoolSystem` code both ways. The difference is who holds the instance.

**Static facade** — nothing to inject, nothing in the scene:

```csharp
Pool.Register(prefab, 20);
var obj = Pool.GetObj<Bullet>();
```

**Constructed / injected** — for DI, tests, or several independent pools:

```csharp
var pool = new PoolSystem();

// VContainer
builder.Register<ISpawner, VContainerSpawner>(Lifetime.Singleton);
builder.Register<PoolSystem>(Lifetime.Singleton).AsSelf();
```

`PoolSystem` has one constructor and no `[Inject]` attribute, so DI frameworks pick it up while the
class stays free of any DI reference.

### The two modes are mutually exclusive

Mixing them would silently give you two disconnected pools, so it throws instead:

```csharp
Pool.GetObj<Bullet>();     // static mode claimed
new PoolSystem();          // InvalidOperationException
```

Several *constructed* pools are fine — only mixing the two **modes** throws.

> Resets on domain reload, i.e. every Play. If you disable **Reload Domain** in *Enter Play Mode
> Options*, the mode survives between Play sessions and the second run throws.

Need the instance from the static side — `Pool.GetPoolSystem()`. A method, not a property: it
creates the singleton, locks in static mode and can throw, none of which should fire because a
debugger evaluated a property on hover.

---

## API

`Pool.X` forwards to the identical `pool.X`.

| Method | Notes |
|---|---|
| `Register<T>(prefab, initAmount)` | Lazy — records only. Instances are created on first use |
| `GetObj<T>()` | `IPoolable` |
| `GetObj<T>(subKey)` | `ISubKeyPoolable` |
| `ReleaseObj(obj, ignoreParentPool = false, worldPosStay = true)` | Returns an object to its pool |
| `DestroyPool<T>()` | Every subKey of `T` |
| `DestroyPool<T>(subKey)` | One variant |
| `DestroyAllPools()` | Everything |

`GetObj` **returns `null`, it does not throw**, when nothing is registered — it logs
`Pool config not found: <Type>/<subKey>` first.

`ignoreParentPool: true` leaves a released object where it is in the hierarchy instead of
re-parenting it under the pool container. `worldPosStay` is passed straight to `SetParent`.

---

## Releasing: two ways

Both reach the same code.

**1. Through the pool** — no component lookup, uses an instance-ID dictionary:

```csharp
Pool.ReleaseObj(bullet);      // static
pool.ReleaseObj(bullet);      // instance
```

**2. The object releases itself** — `Release()` is an extension on `IPoolableBase`, so a pooled
object needs no pool reference at all:

```csharp
public class Bullet : MonoBehaviour, IPoolable
{
    private float life;

    private void Update()
    {
        life += Time.deltaTime;
        if (life > 3f) this.Release();
    }

    public void OnGet() { life = 0f; gameObject.SetActive(true); }
    public void OnRelease() => gameObject.SetActive(false);
}
```

It works because every spawned instance carries an internal `PoolableTracker` recording which
`PoolSystem` created it — not a global lookup, so it still routes correctly with several pools
alive. The cost is one `GetComponent` per call.

| | `pool.ReleaseObj(obj)` | `this.Release()` |
|---|---|---|
| Needs a pool reference | yes | no |
| Component lookup | none | one `GetComponent` |
| Use for | spawners, controllers | self-expiring objects, effects, projectiles |

Releasing twice is safe (logs, does nothing). Releasing something that never came from a pool logs
a warning and does nothing.

---

## Supplying prefabs

`Register()` takes a `Component`. **Where it came from is entirely your business** — the pool has
no loader and no opinion. So any supply mechanism works: a ScriptableObject, Addressables,
`Resources.Load`, a factory, a plain field.

Only one is built in.

### `SO_AllPoolData` — built in

Create → **Pool → PoolData** per entry, then one Create → **Pool → AllPoolData** collecting them.
Each `SO_PoolData` is either `Single` (one `IPoolable` prefab) or `Multiple` (a list of
`ISubKeyPoolable` variants); the Inspector shows only the fields for the mode you pick, and rejects
a prefab that doesn't match it.

```csharp
[SerializeField] private SO_AllPoolData poolConfig;

private void Awake() => poolConfig.ApplyTo(Pool.GetPoolSystem());   // static
```

```csharp
[Inject] public void Construct(PoolSystem pool) => poolConfig.ApplyTo(pool);   // DI
```

`ApplyTo` just walks the list calling `Register()`. The pool never learns `SO_AllPoolData` exists.

### Anything else — you write it

Everything else is a few lines you own. Addressables, for example — resolve the prefab by key, then
`Register()` normally:

```csharp
public static class AddressablePoolLoader
{
    public static async Task RegisterAsync<T>(
        this PoolSystem pool, string key, int initAmount) where T : Component
    {
        var handle = Addressables.LoadAssetAsync<GameObject>(key);
        var prefab = await handle.Task;
        pool.Register(prefab.GetComponent<T>(), initAmount);
    }
}
```

```csharp
await Pool.GetPoolSystem().RegisterAsync<Bullet>("bullet_basic", 20);
var bullet = Pool.GetObj<Bullet>();
```

Only the lookup is async — registering, prewarming, `GetObj` and `ReleaseObj` are unchanged. There
is no Addressables-specific `ISpawner`, because instances are created from a prefab you already
hold.

Two things to get right:

- **Await before the first `GetObj`.** Registration is async, `GetObj` is not.
- **Don't release the handle while the pool lives.** `Register()` keeps the prefab reference and
  instantiates from it whenever the pool grows. Release the handle only after `DestroyPool<T>()`,
  never before.

> Don't put Addressable prefabs into `SO_PoolData`. Its `Prefab` field is a direct reference, so
> the asset gets pulled into the build as a hard dependency — which is exactly what Addressables
> exists to avoid.

---

## Spawner: how instances get created

`ISpawner` is the one seam for "turn this prefab into an instance":

```csharp
public interface ISpawner
{
    T Spawn<T>(T prefab) where T : Component;
}
```

The default is `DefaultSpawner` — plain `Object.Instantiate`. You never need to touch it unless
something else should create your objects.

```csharp
Pool.Spawner = new MySpawner();                 // static — before the first pool call
var pool = new PoolSystem(new MySpawner());     // constructed
```

**`Spawn` is called only when the pool creates a *new* instance** — during prewarm, and when
`GetObj` finds the pool empty. Reusing a released object never touches the spawner, so whatever you
do in here is off the hot path.

**The static `Pool.Spawner` is set-once.** Assign it before the first `Register`/`GetObj`, or the
setter throws — the pool already exists by then and swapping underneath it would be a lie.

### Why you'd replace it

The main reason is dependency injection: `Object.Instantiate` does not resolve `[Inject]` members
on the new instance, so a pooled prefab with its own dependencies comes out half-initialised. The
VContainer sample fixes exactly that:

```csharp
public class VContainerSpawner : ISpawner
{
    private readonly IObjectResolver resolver;

    [Inject]
    public VContainerSpawner(IObjectResolver resolver) => this.resolver = resolver;

    public T Spawn<T>(T prefab) where T : Component => resolver.Instantiate(prefab);
}
```

```csharp
public class Bullet : MonoBehaviour, IPoolable
{
    [Inject] private IScoreService score;   // null with DefaultSpawner, resolved with the above
}
```

Other uses: logging or profiling every instantiation, routing creation through your own factory, or
placing instances in a specific scene. Swapping spawners never touches `PoolSystem` — it only ever
calls `Spawn`.

---

## Lifetime and cleanup are yours

**The pool never shrinks and never cleans itself up.** This is deliberate, and it is the thing most
likely to bite you.

| Event | What happens |
|---|---|
| `Register(prefab, 20)` | Nothing yet — lazy |
| First `GetObj<T>()` | Container created, 20 instances prewarmed |
| `GetObj<T>()` on an empty pool | **Creates one more.** The pool grows |
| `ReleaseObj(obj)` | Deactivated and parked. **Still in memory** |
| Scene unload | **Nothing** — the pool root is `DontDestroyOnLoad` |
| DI scope disposed | **Nothing** — `PoolSystem` is not `IDisposable` |

The pool grows to your **peak concurrent usage** and stays there. A boss fight that spawns 300
projectiles once keeps 300 instances alive until you say otherwise — including in the main menu
three scenes later.

**Release is not destroy.** `ReleaseObj` returns an object for reuse; only `DestroyPool` /
`DestroyAllPools` gives the memory back.

### Why `DontDestroyOnLoad`

A scene unloading is not a statement that you are done with the pool. Treating it as one would also
break things: the pool's entries would point at destroyed containers and the next `GetObj` would
throw `MissingReferenceException`. So the pool survives scene loads on purpose, and the next scene
reuses it already warmed up.

### Your side of the deal

```csharp
Pool.DestroyPool<Bullet>();          // one type, every subKey
Pool.DestroyPool<Sphere>("Red");     // one variant
Pool.DestroyAllPools();              // everything
```

`DestroyPool` destroys parked *and* checked-out instances, including any you re-parented elsewhere.

- **`DestroyPool<T>()`** when a feature is done with a type — leaving a level, closing a mode.
- **`DestroyAllPools()`** when switching major sections, or at teardown.
- **Nothing at all** for genuinely global objects — UI popups, hit effects, damage numbers. Those
  *should* live for the session.

DI users get no cleanup for free, and each `PoolSystem` owns its own `DontDestroyOnLoad` root — so
recreating a scope without cleaning up strands an unreachable one every time:

```csharp
protected override void OnDestroy()
{
    // Resolve before base.OnDestroy(), which disposes the container.
    if (Container != null && Container.TryResolve<PoolSystem>(out var pool))
        pool.DestroyAllPools();

    base.OnDestroy();
}
```

| Symptom | Cause |
|---|---|
| Memory creeps up every level | Pools grew to peak and were never destroyed |
| Several `[PoolSystem]` roots in DontDestroyOnLoad | A DI scope was recreated without `DestroyAllPools()` |
| Assets never unload with Addressables | A pooled prefab still holds the handle, or `OnRelease` doesn't clear references |

---

## Samples

Package Manager → **VegetaPool** → **Samples** → **Import**. The same demo built both ways, so you
can read them side by side.

| Sample | Needs | Shows |
|---|---|---|
| **Static Pool Demo** | nothing | Buttons spawning cubes and colour-keyed spheres via `Pool.*`, configured by `SO_AllPoolData` |
| **VContainer Pool Demo** | VContainer | The same scene via a `LifetimeScope`, plus an `ISpawner` on `IObjectResolver.Instantiate` |

**Install VContainer before importing the second one:**

```
https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer
```

Its `.asmdef` references `VContainer` by name, so importing without it leaves that reference
unresolved. The `.asmdef` is also what contains the damage — without one these scripts would land
in `Assembly-CSharp` and break your whole project instead of just the sample.

Open one scene at a time: running both in a single Play session trips the
[mutual-exclusion guard](#the-two-modes-are-mutually-exclusive).

---

## Design notes

- **Zero dependencies in the core.** `PoolSystem` takes an `ISpawner` and nothing else. The
  VContainer adapter lives inside its sample so installing the package never forces a DI framework
  on you.
- **The two modes are mutually exclusive**, enforced by one `PoolUsageMode` field that never
  returns to `None` once claimed — using both throws instead of silently producing two disconnected
  pools.
- **`this.Release()` needs no `PoolSystem` reference** because each instance carries a
  `PoolableTracker` recording which pool spawned it, so it resolves correctly with several alive.
- **Hot-path bookkeeping is keyed by `GetInstanceID()`** in a dictionary, never `GetComponent` —
  which is why `PoolableTracker` exists instead of storing state on your script.
- **Cleanup is always explicit.** `PoolSystem` is deliberately not `IDisposable`: cleanup is
  something you say, not something that happens to you.

## About

[VegetaAlpha](https://github.com/VegetaAlpha) · `com.vegetaalpha.pool` · see also
[VegetaSystem](https://github.com/VegetaAlpha/VegetaSystem) for the larger framework.
