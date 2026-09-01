using UnityEngine;

namespace VegetaSystem.Samples.Static
{
    public class Sample_Cube : MonoBehaviour, IPoolable
    {
        [SerializeField] private float speedRotate;
        private bool isActive;
        private float time;

        public void OnGet()
        {
            gameObject.SetActive(true);
            time = 0;
        }
        public void OnRelease()
        {
            gameObject.SetActive(false);
            isActive = false;
            time = 0;
        }

        public void SetActive(bool value) => isActive = value;

        private void Update()
        {
            if (!isActive) return;
            time += Time.deltaTime;
            if(time > 2) this.Release(); // Auto release but recommend use pool.ReleaseObj
            transform.Rotate(Vector3.up * speedRotate * Time.deltaTime);
        }

        public void CallCube() { }
    }
}
