public interface ICombat
{
    bool IsHitboxActive();
    float AttackDamage { get; }

    bool IsParrying { get; }
    bool IsParryable { get; }
    void OnParried();
    void Parry();
}