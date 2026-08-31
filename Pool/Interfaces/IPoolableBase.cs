namespace VegetaSystem
{
    /// <summary>Shared by IPoolable and ISubKeyPoolable so shared code doesn't need to check which one.</summary>
    public interface IPoolableBase
    {
        void OnGet();
        void OnRelease();
    }
}
