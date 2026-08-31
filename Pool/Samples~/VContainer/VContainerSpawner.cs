using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace VegetaSystem
{
    /// <summary>
    /// ISpawner backed by VContainer's IObjectResolver.Instantiate — the spawned instance gets its
    /// own [Inject] dependencies resolved, unlike a plain UnityEngine.Object.Instantiate.
    /// </summary>
    public class VContainerSpawner : ISpawner
    {
        private readonly IObjectResolver resolver;

        [Inject]
        public VContainerSpawner(IObjectResolver resolver) => this.resolver = resolver;

        public T Spawn<T>(T prefab) where T : Component => resolver.Instantiate(prefab);
    }
}
