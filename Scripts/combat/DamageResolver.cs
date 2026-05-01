using UnityEngine;

public static class DamageResolver
{
    public static bool TryGetDamageable(Component source, out IDamageable damageable)
    {
        damageable = null;
        if (source == null)
            return false;

        // Scan parents so hits on child colliders still resolve to the owning damageable root.
        MonoBehaviour[] behaviours = source.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IDamageable found)
            {
                damageable = found;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetDamageable(Collider hit, out IDamageable damageable)
    {
        return TryGetDamageable((Component)hit, out damageable);
    }
}
