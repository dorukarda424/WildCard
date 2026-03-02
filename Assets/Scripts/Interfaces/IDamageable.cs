/// <summary>
/// Interface for anything that can take damage (players, destructibles, etc.)
/// </summary>
public interface IDamageable
{
    void TakeDamage(float amount, int attackerViewID);
}
