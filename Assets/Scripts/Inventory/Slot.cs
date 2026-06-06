using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Slot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public bool isHovering;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI amountText;

    private ItemSo _heldItem;
    private int _itemAmount;

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
    }

    public ItemSo GetItem()
    {
        return _heldItem;
    }

    public void SetItem(ItemSo heldItem, int amount)
    {
        _heldItem = heldItem;
        _itemAmount = amount;

        UpdateSlot();
    }

    public int GetItemAmount()
    {
        return _itemAmount;
    }

    public void UpdateSlot()
    {
        if (_heldItem != null)
        {
            iconImage.enabled = true;
            iconImage.sprite = _heldItem.itemSprite;
            amountText.text = _itemAmount.ToString();
        }
        else
        {
            iconImage.enabled = false;
            amountText.text = "";
        }
    }

    public int AddAmount(int amountToAdd)
    {
        _itemAmount += amountToAdd;
        UpdateSlot();
        return _itemAmount;
    }

    public int RemoveAmount(int amountToRemove)
    {
        _itemAmount -= amountToRemove;
        if (_itemAmount <= 0)
            ClearSlot();
        else
            UpdateSlot();
        return _itemAmount;
    }

    public void ClearSlot()
    {
        _heldItem = null;
        _itemAmount = 0;

        UpdateSlot();
    }

    public bool HasItem()
    {
        return _heldItem != null;
    }
}