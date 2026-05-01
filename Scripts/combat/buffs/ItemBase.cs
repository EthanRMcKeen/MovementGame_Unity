using UnityEngine;

public abstract class ItemBase : MonoBehaviour
{
    public ItemController Controller { get; private set; }
    public ItemDefinition Definition { get; private set; }

    public virtual void Initialize(ItemController controller, ItemDefinition definition)
    {
        Controller = controller;
        Definition = definition;
    }

    public abstract void ApplyBuff();
    public abstract void RemoveBuff();
}