using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace VegetaSystem.Samples.Injection
{
    /// <summary>The static sample's controller with `Pool.` swapped for `pool.` — read them side by side.</summary>
    public class Sample_VContainerGameController : MonoBehaviour
    {
        [Header("Pool config")]
        [SerializeField] private SO_AllPoolData poolConfig;

        [Header("Buttons")]
        [SerializeField] private Button spawnCubeBtn;
        [SerializeField] private Button spawnRedSphereBtn;
        [SerializeField] private Button spawnBlueSphereBtn;
        [SerializeField] private Button releaseAllBtn;

        private readonly List<Sample_Cube> spawnedCube = new();
        private readonly List<Sample_Sphere> spawnedSphere = new();

        private PoolSystem pool;

        // Runs during the scope's Awake, so the config is in place before Start wires buttons.
        [Inject]
        public void Construct(PoolSystem pool)
        {
            this.pool = pool;
            poolConfig.ApplyTo(pool);
        }

        private void Start()
        {
            spawnCubeBtn.onClick.AddListener(SpawnCube);
            spawnRedSphereBtn.onClick.AddListener(SpawnRedSphere);
            spawnBlueSphereBtn.onClick.AddListener(SpawnBlueSphere);
            releaseAllBtn.onClick.AddListener(ReleaseAll);
        }

        private void OnDestroy()
        {
            spawnCubeBtn.onClick.RemoveListener(SpawnCube);
            spawnRedSphereBtn.onClick.RemoveListener(SpawnRedSphere);
            spawnBlueSphereBtn.onClick.RemoveListener(SpawnBlueSphere);
            releaseAllBtn.onClick.RemoveListener(ReleaseAll);

            // Release only; destroying this scope's pool is the LifetimeScope's job.
            ReleaseAll();
        }

        private void SpawnCube()
        {
            var cube = pool.GetObj<Sample_Cube>();
            cube.CallCube();

            cube.transform.position = RandomPoint();
            cube.SetActive(true);
            spawnedCube.Add(cube);
        }

        private void SpawnRedSphere() => SpawnSphere(Sample_SphereType.Red);

        private void SpawnBlueSphere() => SpawnSphere(Sample_SphereType.Blue);

        private void SpawnSphere(Sample_SphereType type)
        {
            var sphere = pool.GetObj<Sample_Sphere>(type.ToString());
            sphere.CallSphere();

            sphere.transform.position = RandomPoint();
            sphere.SetActive(true);
            spawnedSphere.Add(sphere);
        }

        private void ReleaseAll()
        {
            foreach (var obj in spawnedCube)
                pool.ReleaseObj(obj);

            foreach (var obj in spawnedSphere)
                pool.ReleaseObj(obj);

            spawnedCube.Clear();
            spawnedSphere.Clear();
        }

        private static Vector3 RandomPoint()
            => new Vector3(Random.Range(-3f, 3f), Random.Range(0f, 3f), Random.Range(-6, -5));
    }
}
