public interface IDamageable
{
    void TakeDamage(float damage);
    bool IsBlocking { get; }
}
