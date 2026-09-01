using UnityEngine;

namespace VegetaSystem.Samples.Static
{
    public class Sample_Sphere : MonoBehaviour, ISubKeyPoolable
    {
        [SerializeField] private Sample_SphereType sphereType;
        [SerializeField] private float speedRotate;
        private bool isActive;
        private float time;

        public void OnGet()
        {
            time = 0;
            gameObject.SetActive(true);
        }
        public void OnRelease()
        {
            time = 0;
            gameObject.SetActive(false);
            isActive = false;
        }

        public string GetSubKeyPool() => sphereType.ToString();

        public void SetActive(bool value) => isActive = value;

        private void Update()
        {
            if (!isActive) return;
            time += Time.deltaTime;
            if(time > 2) this.Release();
            transform.Rotate(Vector3.up * speedRotate * Time.deltaTime);
        }

        public void CallSphere() { }
    }

    public enum Sample_SphereType
    {
        Red,
        Blue,
        Yellow
    }
}
