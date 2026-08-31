using VContainer;
using VContainer.Unity;

namespace VegetaSystem.Samples.Injection
{
    /// <summary>DI counterpart to the static sample: same demo, but nothing touches the static Pool class.</summary>
    public class Sample_VContainerLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Spawning via IObjectResolver is what gets pooled prefabs their own [Inject] deps.
            builder.Register<ISpawner, Sample_VContainerSpawner>(Lifetime.Singleton);
            builder.Register<PoolSystem>(Lifetime.Singleton).AsSelf();

            builder.RegisterComponentInHierarchy<Sample_VContainerGameController>();
        }

        protected override void OnDestroy()
        {
            // The pool root is DontDestroyOnLoad, so without this every scene reload strands
            // another one. Resolve before base.OnDestroy(), which disposes the container.
            if (Container != null && Container.TryResolve<PoolSystem>(out var pool))
                pool.DestroyAllPools();

            base.OnDestroy();
        }
    }
}
