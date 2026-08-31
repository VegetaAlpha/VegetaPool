namespace VegetaSystem
{
    /// <summary>
    /// For a type with several variants, each its own pool. Deliberately not an IPoolable, so the
    /// two GetObj/DestroyPool overloads can't be mixed up.
    /// </summary>
    public interface ISubKeyPoolable : IPoolableBase
    {
        string GetSubKeyPool();
    }
}
