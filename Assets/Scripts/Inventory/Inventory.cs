using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Inventory : MonoBehaviour
{
    [SerializeField] private GameObject hotBarObj;
    [SerializeField] private GameObject inventorySlotParent;
    [SerializeField] private GameObject container;

    [SerializeField] private Image dragIcon;

    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private Material highlightMaterial;

    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform hand;
    [SerializeField] private InputActionReference inventoryAction;
    [SerializeField] private InputActionReference pickUpAction;
    [SerializeField] private InputActionReference dropAction;

    [Header("Hotbar key references")]
    [SerializeField] private InputActionReference hb0Action;
    [SerializeField] private InputActionReference hb1Action;
    [SerializeField] private InputActionReference hb2Action;
    [SerializeField] private InputActionReference hb3Action;
    [SerializeField] private InputActionReference hb4Action;
    [SerializeField] private InputActionReference hb5Action;
    public float equippedOpacity = 0.9f;
    public float normalOpacity = 0.5f;

    private readonly List<Slot> _allSlots = new();
    private readonly List<Slot> _hotbarSlots = new();

    private readonly List<Slot> _inventorySlots = new();

    private Slot _draggingSlot;

    private int _equippedHotbarIndex;
    private bool _isDragging;
    private Renderer _lookedAtRenderer;
    private Material _originalMaterial;
    private GameObject _currentHandItem;
    

    private void Awake()
    {
        _inventorySlots.AddRange(inventorySlotParent.GetComponentsInChildren<Slot>());
        _hotbarSlots.AddRange(hotBarObj.GetComponentsInChildren<Slot>());

        _allSlots.AddRange(_inventorySlots);
        _allSlots.AddRange(_hotbarSlots);
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
        inventoryAction.action.performed += ToggleInventory;
        inventoryAction.action.canceled += ToggleInventory;

        pickUpAction.action.performed += Pickup;

        dropAction.action.performed += Drop;

        hb0Action.action.performed += HandleHotbarKey0Selection;
        hb1Action.action.performed += HandleHotbarKey1Selection;
        hb2Action.action.performed += HandleHotbarKey2Selection;
        hb3Action.action.performed += HandleHotbarKey3Selection;
        hb4Action.action.performed += HandleHotbarKey4Selection;
        hb5Action.action.performed += HandleHotbarKey5Selection;
    }

    private void OnDisable()
    {
        inventoryAction.action.performed -= ToggleInventory;
        inventoryAction.action.canceled -= ToggleInventory;

        pickUpAction.action.performed -= Pickup;

        dropAction.action.performed -= Drop;

        hb0Action.action.performed -= HandleHotbarKey0Selection;
        hb1Action.action.performed -= HandleHotbarKey1Selection;
        hb2Action.action.performed -= HandleHotbarKey2Selection;
        hb3Action.action.performed -= HandleHotbarKey3Selection;
        hb4Action.action.performed -= HandleHotbarKey4Selection;
        hb5Action.action.performed -= HandleHotbarKey5Selection;
    }

    public static event Action<bool> OnInventoryToggled;

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
                _draggingSlot = hovered;
                _isDragging = true;

                dragIcon.sprite = hovered.GetItem().itemSprite;
                dragIcon.color = new Color(1, 1, 1, 0.5f);
                dragIcon.enabled = true;
            }
        }
    }

    private void EndDrag()
    {
        if (Input.GetMouseButtonUp(0) && _isDragging)
        {
            var hovered = GetHoveredSlot();

            if (hovered != null)
            {
                HandleDrop(_draggingSlot, hovered);

                dragIcon.enabled = false;

                _draggingSlot = null;
                _isDragging = false;
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
        if (_isDragging) dragIcon.transform.position = Input.mousePosition;
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
        container.SetActive(!container.activeInHierarchy);
        Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = !Cursor.visible;

        if (!container.activeInHierarchy && _isDragging)
        {
            _isDragging = false;
            _draggingSlot = null;
            dragIcon.enabled = false;
        }

        OnInventoryToggled?.Invoke(container.activeInHierarchy);
    }

    private void HandlePickUp()
    {
        if (_lookedAtRenderer != null)
        {
            var item = _lookedAtRenderer.GetComponent<Item>();
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
        var equippedSlot = _hotbarSlots[_equippedHotbarIndex];

        if (!equippedSlot.HasItem()) return;

        var itemSo = equippedSlot.GetItem();
        var prefab = itemSo.itemPrefab;

        if (prefab == null) return;

        var dropped = Instantiate(
            prefab,
            cameraTransform.position + cameraTransform.forward,
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
        if (_lookedAtRenderer != null)
        {
            _lookedAtRenderer.material = _originalMaterial;
            _lookedAtRenderer = null;
            _originalMaterial = null;
        }

        var ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (Physics.Raycast(ray, out var hit, pickupRange))
        {
            var item = hit.collider.GetComponent<Item>();
            if (item != null)
            {
                var rend = item.GetComponent<Renderer>();
                if (rend != null)
                {
                    _originalMaterial = rend.material;
                    rend.material = highlightMaterial;
                    _lookedAtRenderer = rend;
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
                icon.color = i == _equippedHotbarIndex
                    ? new Color(1, 1, 1, equippedOpacity)
                    : new Color(1, 1, 1, normalOpacity);
        }
    }

    private void HandleHotbarKey0Selection(InputAction.CallbackContext ctx)
    {
        _equippedHotbarIndex = 0;
        UpdateHotbarOpacity();
        EquipHandItem();
    }

    private void HandleHotbarKey1Selection(InputAction.CallbackContext ctx)
    {
        _equippedHotbarIndex = 1;
        UpdateHotbarOpacity();
        EquipHandItem();
    }

    private void HandleHotbarKey2Selection(InputAction.CallbackContext ctx)
    {
        _equippedHotbarIndex = 2;
        UpdateHotbarOpacity();
        EquipHandItem();
    }

    private void HandleHotbarKey3Selection(InputAction.CallbackContext ctx)
    {
        _equippedHotbarIndex = 3;
        UpdateHotbarOpacity();
        EquipHandItem();
    }

    private void HandleHotbarKey4Selection(InputAction.CallbackContext ctx)
    {
        _equippedHotbarIndex = 4;
        UpdateHotbarOpacity();
        EquipHandItem();
    }

    private void HandleHotbarKey5Selection(InputAction.CallbackContext ctx)
    {
        _equippedHotbarIndex = 5;
        UpdateHotbarOpacity();
        EquipHandItem();
    }

    private void EquipHandItem()
    {
        if (_currentHandItem != null)
        {
            Destroy(_currentHandItem.gameObject);
        }
        
        Slot equippedSlot = _hotbarSlots[_equippedHotbarIndex];
        if (!equippedSlot.HasItem()) return;
        
        var item = equippedSlot.GetItem();
        if (item.handItemPrefab == null) return;
        
        _currentHandItem = Instantiate(item.handItemPrefab, hand);
        _currentHandItem.transform.localPosition = Vector3.zero;
        _currentHandItem.transform.localRotation = Quaternion.identity;
    }
}