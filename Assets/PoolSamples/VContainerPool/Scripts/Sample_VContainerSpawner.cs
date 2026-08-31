using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace VegetaSystem.Samples.Injection
{
    /// <summary>ISpawner via IObjectResolver.Instantiate, so the new instance gets its [Inject] deps resolved.</summary>
    public class Sample_VContainerSpawner : ISpawner
    {
        private readonly IObjectResolver resolver;

        [Inject]
        public Sample_VContainerSpawner(IObjectResolver resolver) => this.resolver = resolver;

        public T Spawn<T>(T prefab) where T : Component => resolver.Instantiate(prefab);
    }
}
