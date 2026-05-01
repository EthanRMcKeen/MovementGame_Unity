public interface ICombatHitResponder
{
    void OnAttackBlocked(DamageRequest request, IDamageable defender);
    void OnAttackParried(DamageRequest request, IDamageable defender);
}
