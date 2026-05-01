using UnityEngine;

[CreateAssetMenu(fileName = "ItemDefinition", menuName = "Items/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string _itemId = "item";
    [SerializeField] private string _displayName = "Item";

    [Header("Runtime")]
    [SerializeField] private ItemBase _itemPrefab;

    public string ItemId { get { return string.IsNullOrWhiteSpace(_itemId) ? name : _itemId; } }
    public string DisplayName { get { return string.IsNullOrWhiteSpace(_displayName) ? name : _displayName; } }
    public ItemBase ItemPrefab { get { return _itemPrefab; } }
}