using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Inventory : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject hotBarObject;
    [SerializeField] private GameObject inventorySlotParent;
    [SerializeField] private GameObject inventoryContainer;
    [SerializeField] private Image draggedItemIcon;
    
    [Header("Visual Feedback")]
    [SerializeField] private Material itemHighlightMaterial;

    [Header("Player References")]
    [SerializeField] private Transform playerCameraTransform;
    [SerializeField] private Transform playerHandTransform;
    
    [Header("Input Actions")]
    [SerializeField] private InputActionReference toggleInventoryAction;
    [SerializeField] private InputActionReference pickUpItemAction;
    [SerializeField] private InputActionReference dropItemAction;

    [Header("Hotbar Input Actions")]
    [SerializeField] private InputActionReference[] hotbarActions;

    private const float EquippedSlotOpacity = 0.9f;
    private const float UnequippedSlotOpacity = 0.5f;
    private const float PickupRange = 3f;

    private readonly List<Slot> _allSlots = new();
    private readonly List<Slot> _hotbarSlots = new();
    private readonly List<Slot> _inventorySlots = new();
    
    private int _selectedHotbarIndex;
    private bool _isDraggingItem;
    private Renderer _highlightedItemRenderer;
    private Material _highlightedItemOriginalMaterial;
    private GameObject _currentlyHeldItem;
    private Slot _currentlyDraggedSlot;
    private Action<InputAction.CallbackContext>[] _hotbarCallbacks;
    
    public static event Action<bool> OnInventoryToggled;
    
    private void Awake()
    {
        _inventorySlots.AddRange(inventorySlotParent.GetComponentsInChildren<Slot>());
        _hotbarSlots.AddRange(hotBarObject.GetComponentsInChildren<Slot>());

        _allSlots.AddRange(_inventorySlots);
        _allSlots.AddRange(_hotbarSlots);
        
        _hotbarCallbacks = new Action<InputAction.CallbackContext>[hotbarActions.Length];
        for (var i = 0; i < hotbarActions.Length; i++)
        {
            var index = i;
            _hotbarCallbacks[i] = _ => HandleHotbarKeySelection(index);
        }

    }

    private void Update()
    {
        DetectLookedAtItem();

        StartDrag();
        UpdateDragItemPosition();
        EndDrag();
    }

    private void OnEnable()
    {
        toggleInventoryAction.action.performed += ToggleInventory;
        toggleInventoryAction.action.canceled += ToggleInventory;

        pickUpItemAction.action.performed += Pickup;

        dropItemAction.action.performed += Drop;

        for (var i = 0; i < hotbarActions.Length; i++)
            hotbarActions[i].action.performed += _hotbarCallbacks[i];
    }

    private void OnDisable()
    {
        toggleInventoryAction.action.performed -= ToggleInventory;
        toggleInventoryAction.action.canceled -= ToggleInventory;

        pickUpItemAction.action.performed -= Pickup;

        dropItemAction.action.performed -= Drop;

        for (var i = 0; i < hotbarActions.Length; i++)
            hotbarActions[i].action.performed -= _hotbarCallbacks[i];
    }

    private void AddItem(ItemSo itemToAdd, int amount)
    {
        var remaining = amount;

        foreach (var slot in _allSlots)
            if (slot.HasItem() && slot.GetItem() == itemToAdd)
            {
                var currentAmount = slot.GetItemAmount();
                var maxStack = itemToAdd.itemMaxStack;

                if (currentAmount < maxStack)
                {
                    var spaceLeft = maxStack - currentAmount;
                    var amountToAdd = Mathf.Min(spaceLeft, remaining);

                    slot.SetItem(itemToAdd, currentAmount + amountToAdd);
                    remaining -= amountToAdd;

                    if (remaining <= 0) return;
                }
            }

        foreach (var slot in _allSlots)
            if (!slot.HasItem())
            {
                var amountToPlace = Mathf.Min(itemToAdd.itemMaxStack, remaining);
                slot.SetItem(itemToAdd, amountToPlace);
                remaining -= amountToPlace;

                if (remaining <= 0) return;
            }

        if (remaining > 0) Debug.Log("Inventory Full");
    }

    private void StartDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var hovered = GetHoveredSlot();

            if (hovered != null && hovered.HasItem())
            {
                _currentlyDraggedSlot = hovered;
                _isDraggingItem = true;

                draggedItemIcon.sprite = hovered.GetItem().itemSprite;
                draggedItemIcon.color = new Color(1, 1, 1, 0.5f);
                draggedItemIcon.enabled = true;
            }
        }
    }

    private void EndDrag()
    {
        if (Input.GetMouseButtonUp(0) && _isDraggingItem)
        {
            var hovered = GetHoveredSlot();

            if (hovered != null)
            {
                HandleDrop(_currentlyDraggedSlot, hovered);

                draggedItemIcon.enabled = false;

                _currentlyDraggedSlot = null;
                _isDraggingItem = false;
            }
        }
    }

    private void HandleDrop(Slot from, Slot to)
    {
        if (from == to) return;

        // Stacking
        if (to.HasItem() && to.GetItem() == from.GetItem())
        {
            var max = to.GetItem().itemMaxStack;
            var space = max - to.GetItemAmount();

            if (space > 0)
            {
                var move = Mathf.Min(space, from.GetItemAmount());

                to.SetItem(to.GetItem(), to.GetItemAmount() + move);
                from.SetItem(from.GetItem(), from.GetItemAmount() - move);

                if (from.GetItemAmount() <= 0) from.ClearSlot();

                return;
            }
        }

        // Different Item
        if (to.HasItem())
        {
            var tempItem = to.GetItem();
            var tempAmount = to.GetItemAmount();

            to.SetItem(from.GetItem(), from.GetItemAmount());
            from.SetItem(tempItem, tempAmount);
            return;
        }

        // Empty Slot
        to.SetItem(from.GetItem(), from.GetItemAmount());
        from.ClearSlot();
    }

    private void UpdateDragItemPosition()
    {
        if (_isDraggingItem) draggedItemIcon.transform.position = Input.mousePosition;
    }

    private Slot GetHoveredSlot()
    {
        foreach (var slot in _allSlots)
            if (slot.isHovering)
                return slot;

        return null;
    }

    private void ToggleInventory(InputAction.CallbackContext ctx)
    {
        HandleToggleInventory();
    }

    private void Pickup(InputAction.CallbackContext ctx)
    {
        HandlePickUp();
    }

    private void Drop(InputAction.CallbackContext ctx)
    {
        HandleDropItem();
    }

    private void HandleToggleInventory()
    {
        inventoryContainer.SetActive(!inventoryContainer.activeInHierarchy);
        Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = !Cursor.visible;

        if (!inventoryContainer.activeInHierarchy && _isDraggingItem)
        {
            _isDraggingItem = false;
            _currentlyDraggedSlot = null;
            draggedItemIcon.enabled = false;
        }

        OnInventoryToggled?.Invoke(inventoryContainer.activeInHierarchy);
    }

    private void HandlePickUp()
    {
        if (_highlightedItemRenderer != null)
        {
            var item = _highlightedItemRenderer.GetComponent<Item>();
            if (item != null)
            {
                AddItem(item.item, item.amount);
                Destroy(item.gameObject);
                EquipHandItem();
            }
        }
    }

    private void HandleDropItem()
    {
        var equippedSlot = _hotbarSlots[_selectedHotbarIndex];

        if (!equippedSlot.HasItem()) return;

        var itemSo = equippedSlot.GetItem();
        var prefab = itemSo.itemPrefab;

        if (prefab == null) return;

        var dropped = Instantiate(
            prefab,
            playerCameraTransform.position + playerCameraTransform.forward,
            Quaternion.identity
        );

        var item = dropped.GetComponent<Item>();
        if (item == null) item = dropped.AddComponent<Item>();

        item.item = itemSo;
        item.amount = equippedSlot.GetItemAmount();

        equippedSlot.ClearSlot();
        EquipHandItem();
    }

    private void DetectLookedAtItem()
    {
        if (_highlightedItemRenderer != null)
        {
            _highlightedItemRenderer.material = _highlightedItemOriginalMaterial;
            _highlightedItemRenderer = null;
            _highlightedItemOriginalMaterial = null;
        }

        var ray = new Ray(playerCameraTransform.position, playerCameraTransform.forward);
        if (Physics.Raycast(ray, out var hit, PickupRange))
        {
            var item = hit.collider.GetComponent<Item>();
            if (item != null)
            {
                var rend = item.GetComponent<Renderer>();
                if (rend != null)
                {
                    _highlightedItemOriginalMaterial = rend.material;
                    rend.material = itemHighlightMaterial;
                    _highlightedItemRenderer = rend;
                }
            }
        }
    }

    private void UpdateHotbarOpacity()
    {
        for (var i = 0; i < _hotbarSlots.Count; i++)
        {
            var icon = _hotbarSlots[i].GetComponent<Image>(); // was [1]
            if (icon != null)
                icon.color = i == _selectedHotbarIndex
                    ? new Color(1, 1, 1, EquippedSlotOpacity)
                    : new Color(1, 1, 1, UnequippedSlotOpacity);
        }
    }
    
    private void HandleHotbarKeySelection(int index)
    {
        _selectedHotbarIndex = index;
        UpdateHotbarOpacity();
        EquipHandItem();
    }

    private void EquipHandItem()
    {
        if (_currentlyHeldItem != null)
        {
            Destroy(_currentlyHeldItem.gameObject);
        }
        
        Slot equippedSlot = _hotbarSlots[_selectedHotbarIndex];
        if (!equippedSlot.HasItem()) return;
        
        var item = equippedSlot.GetItem();
        if (item.handItemPrefab == null) return;
        
        _currentlyHeldItem = Instantiate(item.handItemPrefab, playerHandTransform);
        _currentlyHeldItem.transform.localPosition = Vector3.zero;
        _currentlyHeldItem.transform.localRotation = Quaternion.identity;
    }
}