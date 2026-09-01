using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VegetaSystem.Samples.Static
{
    public class Sample_GameController : MonoBehaviour
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

        private void Awake()
        {
            poolConfig.ApplyTo(Pool.GetPoolSystem());
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

            // Release, don't destroy: the pool root is DontDestroyOnLoad and the next scene
            // reuses it warm. Destroying is an explicit call, not a side effect of unloading.
            ReleaseAll();
        }

        private void SpawnCube()
        {
            var cube = Pool.GetObj<Sample_Cube>();
            cube.CallCube();

            cube.transform.position = RandomPoint();
            cube.transform.SetParent(null);
            cube.SetActive(true);
            spawnedCube.Add(cube);
        }

        private void SpawnRedSphere() => SpawnSphere(Sample_SphereType.Red);

        private void SpawnBlueSphere() => SpawnSphere(Sample_SphereType.Blue);

        // Both sphere buttons share one pool type, differing only by subKey — ISubKeyPoolable.
        private void SpawnSphere(Sample_SphereType type)
        {
            var sphere = Pool.GetObj<Sample_Sphere>(type.ToString());
            sphere.CallSphere();

            sphere.transform.position = RandomPoint();
            sphere.SetActive(true);
            spawnedSphere.Add(sphere);
        }

        private void ReleaseAll()
        {
            foreach (var obj in spawnedCube)
                Pool.ReleaseObj(obj);

            foreach (var obj in spawnedSphere)
                Pool.ReleaseObj(obj);

            spawnedCube.Clear();
            spawnedSphere.Clear();
        }

        private static Vector3 RandomPoint()
            => new Vector3(Random.Range(-3f, 3f), Random.Range(0f, 3f), Random.Range(-6, -5));
    }
}
