using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovements : MonoBehaviour
{
    [Header("Speed")] 
    [SerializeField] private float walkingSpeed;
    [SerializeField] private float runningSpeed;
    [SerializeField] private float crouchingSpeed;
    [SerializeField] private float mouseSensitivity;

    [Header("Jump and Fall")] 
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float gravity = -12f;
    [SerializeField] private float initialFallVelocity = -2f;

    [Header("Crouch")] [SerializeField] private float standingHeigh = 2f;

    [SerializeField] private float crouchingHeigh = 1f;
    [SerializeField] private float crouchTransitionSpeed = 10f;
    [SerializeField] private float cameraOffset = 0.2f;

    [Header("References")] 
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private GameObject inventory;
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference crouchAction;
    [SerializeField] private InputActionReference jumpAction;

    private CharacterController _characterController;

    private bool _isCrouching;
    private bool _isGrounded;
    private bool _isInventoryOpened;
    private bool _isSprinting;
    
    private Vector2 _lookInput;
    private Vector2 _moveInput;
    private Vector2 _sprintInput;
    private Vector2 _crouchInput;
    
    private float _targetHeight;
    private float _verticalAngle;
    private float _verticalVelocity;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        _targetHeight = standingHeigh;

        cameraTransform.localPosition = (_characterController.height - cameraOffset) * Vector3.up;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        _isGrounded = _characterController.isGrounded;
        HandleGravity();
        HandleCamera();
        HandleMovement();
        HandleCrouchTransition();
    }

    private void OnEnable()
    {
        moveAction.action.performed += StoreMovementInput;
        moveAction.action.canceled += StoreMovementInput;

        lookAction.action.performed += StoreCameraInput;
        lookAction.action.canceled += StoreCameraInput;

        sprintAction.action.performed += Sprint;
        sprintAction.action.canceled += Sprint;

        crouchAction.action.performed += Crouch;

        jumpAction.action.performed += Jump;

        Inventory.OnInventoryToggled += HandleInventoryToggled;
    }

    private void OnDisable()
    {
        moveAction.action.performed -= StoreMovementInput;
        moveAction.action.canceled -= StoreMovementInput;

        lookAction.action.performed -= StoreCameraInput;
        lookAction.action.canceled -= StoreCameraInput;

        sprintAction.action.performed -= Sprint;
        sprintAction.action.canceled -= Sprint;

        crouchAction.action.performed -= Crouch;

        jumpAction.action.performed -= Jump;

        Inventory.OnInventoryToggled -= HandleInventoryToggled;
    }

    private void OnDrawGizmos()
    {
        if (_characterController == null) return;

        Gizmos.color = CanStandUp() ? Color.green : Color.red;

        var castOriginBottom = transform.position + Vector3.up * _characterController.radius;
        var castOriginTop =
            transform.position + Vector3.up * (_characterController.height - _characterController.radius);

        Gizmos.DrawWireSphere(castOriginBottom, _characterController.radius);
        Gizmos.DrawWireSphere(castOriginTop, _characterController.radius);
        Gizmos.DrawLine(
            castOriginBottom + Vector3.right * _characterController.radius,
            castOriginTop + Vector3.right * _characterController.radius
        );
        Gizmos.DrawLine(
            castOriginBottom - Vector3.right * _characterController.radius,
            castOriginTop - Vector3.right * _characterController.radius
        );
    }

    private void StoreMovementInput(InputAction.CallbackContext ctx)
    {
        _moveInput = ctx.ReadValue<Vector2>();
    }

    private void StoreCameraInput(InputAction.CallbackContext ctx)
    {
        _lookInput = ctx.ReadValue<Vector2>();
    }

    private void Jump(InputAction.CallbackContext ctx)
    {
        if (_isGrounded && !_isInventoryOpened) _verticalVelocity = jumpForce;
    }

    private void Sprint(InputAction.CallbackContext ctx)
    {
        _isSprinting = ctx.performed;
    }

    private void Crouch(InputAction.CallbackContext ctx)
    {
        if (!CanStandUp() || _isInventoryOpened) return;
        _targetHeight = _isCrouching ? standingHeigh : crouchingHeigh;
        _isCrouching = !_isCrouching;
    }

    private void HandleMovement()
    {
        var finalMove = Vector3.zero;
        if (!_isInventoryOpened)
        {
            var move = cameraTransform.TransformDirection(_moveInput.x, 0, _moveInput.y).normalized;
            var currentSpeed = _isCrouching ? crouchingSpeed : _isSprinting ? runningSpeed : walkingSpeed;
            finalMove = move * currentSpeed;
        }

        finalMove.y = _verticalVelocity;

        var collisions = _characterController.Move(finalMove * Time.deltaTime);
        if ((collisions & CollisionFlags.Above) != 0) _verticalVelocity = initialFallVelocity;
    }

    private void HandleCrouchTransition()
    {
        var currentHeight = _characterController.height;
        if (Mathf.Abs(currentHeight - _targetHeight) < 0.01f)
        {
            _characterController.height = _targetHeight;
            _characterController.center = Vector3.up * (_characterController.height * 0.5f);
            return;
        }

        var newHeight = Mathf.Lerp(currentHeight, _targetHeight, crouchTransitionSpeed * Time.deltaTime);
        _characterController.height = newHeight;
        _characterController.center = Vector3.up * (newHeight * 0.5f);

        var cameraTargetPosition = cameraTransform.localPosition;
        cameraTargetPosition.y = _targetHeight - cameraOffset;
        cameraTransform.localPosition = Vector3.Lerp(
            cameraTransform.localPosition,
            cameraTargetPosition,
            crouchTransitionSpeed * Time.deltaTime
        );
    }

    private void HandleCamera()
    {
        if (_isInventoryOpened) return;

        transform.Rotate(0, _lookInput.x * mouseSensitivity, 0);

        _verticalAngle -= _lookInput.y * mouseSensitivity;
        _verticalAngle = Mathf.Clamp(_verticalAngle, -90f, 90f);

        cameraTransform.localEulerAngles = new Vector3(_verticalAngle, 0, 0);
    }

    private void HandleGravity()
    {
        if (_isGrounded && _verticalVelocity < 0) _verticalVelocity = initialFallVelocity;
        _verticalVelocity += gravity * Time.deltaTime;
    }

    private void HandleInventoryToggled(bool isOpen)
    {
        _isInventoryOpened = isOpen;
    }

    private bool CanStandUp()
    {
        if (!_isCrouching) return true;

        var heightDifference = standingHeigh - crouchingHeigh;
        var castOriginBottom = transform.position + Vector3.up * _characterController.radius;
        var castOriginTop =
            transform.position + Vector3.up * (_characterController.height - _characterController.radius);

        return !Physics.CapsuleCast(
            castOriginBottom,
            castOriginTop,
            _characterController.radius,
            Vector3.up,
            heightDifference
        );
    }
}