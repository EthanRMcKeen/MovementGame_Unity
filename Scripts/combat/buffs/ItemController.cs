using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class ItemController : NetworkBehaviour
{
    [Header("Loadout")]
    [SerializeField] private ItemDefinition[] _loadout;

    private readonly List<ItemBase> _itemInstances = new List<ItemBase>();

    private void Awake()
    {
        // Initialize all items in loadout
        foreach (var itemDef in _loadout)
        {
            if (itemDef != null)
            {
                AddItem(itemDef);
            }
        }
    }

    public void AddItem(ItemDefinition itemDef)
    {
        if (itemDef == null)
            return;

        // Instantiate the item
        ItemBase itemInstance = null;
        if (itemDef.ItemPrefab != null)
        {
            itemInstance = Instantiate(itemDef.ItemPrefab, transform);
            itemInstance.Initialize(this, itemDef);
        }
        else
        {
            // For now, assume prefab is required
            Debug.LogError("ItemDefinition must have ItemPrefab set.");
            return;
        }

        _itemInstances.Add(itemInstance);
        itemInstance.ApplyBuff();
    }

    public void RemoveItem(ItemBase itemInstance)
    {
        if (itemInstance == null || !_itemInstances.Contains(itemInstance))
            return;

        itemInstance.RemoveBuff();
        Destroy(itemInstance.gameObject);
        _itemInstances.Remove(itemInstance);
    }

    public IEnumerable<ItemBase> GetItemInstances()
    {
        return _itemInstances;
    }
}