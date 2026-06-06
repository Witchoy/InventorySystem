using UnityEngine;

[CreateAssetMenu(fileName = "Item", menuName = "Inventory/ItemSo")]
public class ItemSo : ScriptableObject
{
    [SerializeField] private string itemName;
    [SerializeField] public Sprite itemSprite;
    [SerializeField] public int itemMaxStack;
    [SerializeField] public GameObject itemPrefab;
    [SerializeField] public GameObject itemHandPrefab;
}