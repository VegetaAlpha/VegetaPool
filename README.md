# VegetaPool

Object pooling for Unity. No dependencies. Static facade or injected instance — same API.

```
https://github.com/VegetaAlpha/VegetaPool.git?path=Pool
```

Package Manager → **+** → **Install package from git URL…** · Unity 2022.3+ · no tags yet, so this
tracks `main`.

---

## 1. Make something poolable

```csharp
using UnityEngine;
using VegetaSystem;

public class Bullet : MonoBehaviour, IPoolable
{
    public void OnGet()     => gameObject.SetActive(true);
    public void OnRelease() => gameObject.SetActive(false);
}
```

> **Clear references in `OnRelease`.** Anything a pooled object still points at stays in memory as
> long as the pool does.

### Variants — `ISubKeyPoolable`

One class, several interchangeable prefabs, each its own pool.

```csharp
public enum SphereType { Red, Blue, Yellow }

public class Sphere : MonoBehaviour, ISubKeyPoolable
{
    [SerializeField] private SphereType sphereType;   // set per prefab

    public string GetSubKeyPool() => sphereType.ToString();

    public void OnGet()     => gameObject.SetActive(true);
    public void OnRelease() => gameObject.SetActive(false);
}
```

> Pools are keyed by `prefab.GetType().Name` — the **runtime** type, not the compile-time `T`.

---

## 2. Pick a mode

**Static:**

```csharp
Pool.Register(bulletPrefab, initAmount: 20);
var bullet = Pool.GetObj<Bullet>();
```

**Injected:**

```csharp
builder.Register<ISpawner, VContainerSpawner>(Lifetime.Singleton);
builder.Register<PoolSystem>(Lifetime.Singleton).AsSelf();
```

```csharp
[Inject] public void Construct(PoolSystem pool) => this.pool = pool;
var bullet = pool.GetObj<Bullet>();
```

> **The two modes are mutually exclusive** — using both throws `InvalidOperationException` instead
> of silently giving you two disconnected pools. Several *injected* pools are fine.
>
> The claim resets on domain reload. If you turn off **Reload Domain** in *Enter Play Mode
> Options*, it survives between Play sessions and your second run throws.

Need the instance from the static side: `Pool.GetPoolSystem()`.

---

## 3. API

`Pool.X` forwards to the identical `pool.X`.

| | |
|---|---|
| `Register<T>(prefab, initAmount)` | Lazy — instances are created on first use |
| `GetObj<T>()` | `IPoolable` |
| `GetObj<T>(subKey)` | `ISubKeyPoolable` |
| `ReleaseObj(obj, ignoreParentPool = false, worldPosStay = true)` | Back to the pool |
| `DestroyPool<T>()` / `DestroyPool<T>(subKey)` | Destroy one type / one variant |
| `DestroyAllPools()` | Destroy everything |

> `GetObj` **returns `null`, it does not throw**, when nothing is registered. It logs
> `Pool config not found: <Type>/<subKey>` first.

---

## 4. Release — two ways

```csharp
Pool.ReleaseObj(bullet);      // or pool.ReleaseObj(bullet)
```

```csharp
this.Release();               // from inside the object, no pool reference needed
bullet.Release();             // same extension, from outside
```

| | `ReleaseObj(obj)` | `this.Release()` |
|---|---|---|
| Needs a pool reference | yes | no |
| Cost | dictionary lookup | one `GetComponent` |
| Use for | spawners, controllers | self-expiring objects, effects, projectiles |

> Releasing twice is safe. Releasing something that never came from a pool logs a warning and does
> nothing.

---

## 5. Supplying prefabs

`Register()` takes a `Component` — **where it came from is your business.** ScriptableObject,
Addressables, `Resources.Load`, a factory, a plain field: all fine. Only the first is built in.

### `SO_AllPoolData` — built in

Create → **Pool → PoolData** per entry, then Create → **Pool → AllPoolData** collecting them. Each
entry is `Single` (one `IPoolable`) or `Multiple` (a list of `ISubKeyPoolable` variants).

```csharp
[SerializeField] private SO_AllPoolData poolConfig;

private void Awake() => poolConfig.ApplyTo(Pool.GetPoolSystem());   // static
```

```csharp
[Inject] public void Construct(PoolSystem pool) => poolConfig.ApplyTo(pool);   // injected
```

### Everything else — you write it

Addressables, for example:

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

> - **Await before the first `GetObj`.** Registration is async, `GetObj` is not.
> - **Don't release the handle while the pool lives.** `Register()` keeps the prefab and
>   instantiates from it every time the pool grows. Release only after `DestroyPool<T>()`.
> - **Don't put Addressable prefabs in `SO_PoolData`.** Its `Prefab` field is a direct reference,
>   so the asset gets pulled into the build as a hard dependency — defeating Addressables.

---

## 6. Spawner — how instances get created

```csharp
public interface ISpawner
{
    T Spawn<T>(T prefab) where T : Component;
}
```

Default is `DefaultSpawner` (plain `Object.Instantiate`). Replace it when something else should
create your objects:

```csharp
Pool.Spawner = new MySpawner();                 // static
var pool = new PoolSystem(new MySpawner());     // injected
```

The main reason to replace it is DI — `Object.Instantiate` does not resolve `[Inject]` members, so
a pooled prefab with dependencies comes out half-initialised:

```csharp
public class VContainerSpawner : ISpawner
{
    private readonly IObjectResolver resolver;

    [Inject]
    public VContainerSpawner(IObjectResolver resolver) => this.resolver = resolver;

    public T Spawn<T>(T prefab) where T : Component => resolver.Instantiate(prefab);
}
```

> - **`Pool.Spawner` is set-once** — assign it before the first `Register`/`GetObj` or the setter
>   throws.
> - **`Spawn` only runs when the pool creates a new instance** (prewarm, or `GetObj` on an empty
>   pool). Reuse never touches it, so this is off the hot path.

---

## 7. Lifetime — you own it

**The pool never shrinks and never cleans itself up.** That is deliberate: destroying pooled
objects is your decision, not a side effect of a scene unloading.

| Event | What the pool does |
|---|---|
| `Register(prefab, 20)` | Nothing yet — lazy |
| First `GetObj<T>()` | Container created, 20 prewarmed |
| `GetObj<T>()` on an empty pool | **Creates one more.** The pool grows |
| `ReleaseObj(obj)` | Deactivated and parked. **Still in memory** |
| Scene unload | **Nothing** — the pool root is `DontDestroyOnLoad` |
| DI scope disposed | **Nothing** — `PoolSystem` is not `IDisposable` |

```csharp
Pool.DestroyPool<Bullet>();          // one type, every subKey
Pool.DestroyPool<Sphere>("Red");     // one variant
Pool.DestroyAllPools();              // everything
```

- **`DestroyPool<T>()`** when a feature is done with a type — leaving a level, closing a mode.
- **`DestroyAllPools()`** when switching major sections, or at teardown.
- **Nothing at all** for genuinely global objects — UI popups, hit effects, damage numbers.

> - **Release is not destroy.** `ReleaseObj` parks an object for reuse; only `DestroyPool` /
>   `DestroyAllPools` gives the memory back.
> - **The pool grows to your peak concurrent usage and stays there.** 300 projectiles in one boss
>   fight means 300 instances alive in the main menu three scenes later.
> - **`DestroyPool` also destroys checked-out instances**, including ones you re-parented
>   elsewhere.

**Injected pools get no cleanup for free**, and each owns its own `DontDestroyOnLoad` root — so
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

---

## 8. Samples

Package Manager → **VegetaPool** → **Samples** → **Import**. Same demo, built both ways.

| Sample | Needs |
|---|---|
| **Static Pool Demo** | nothing |
| **VContainer Pool Demo** | VContainer |

```
https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer
```

> - **Install VContainer *before* importing the second sample** — its `.asmdef` references
>   `VContainer` by name and won't resolve otherwise.
> - **Open one scene at a time.** Running both in a single Play session trips the mode guard.

---

[VegetaAlpha](https://github.com/VegetaAlpha) · `com.vegetaalpha.pool` · part of
[VegetaSystem](https://github.com/VegetaAlpha/VegetaSystem)
