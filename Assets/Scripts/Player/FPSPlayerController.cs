using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 6.0f;
    public float runSpeed = 10.0f;
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    [Header("Look Settings")]
    public Camera playerCamera;
    public float lookSpeed = 2.0f;
    public float lookXLimit = 80.0f;

    [Header("Keybindings")]
    public KeyCode runKey = KeyCode.LeftShift;

    private CharacterController characterController;
    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0;
    private float currentSpeedX = 0;
    private float currentSpeedY = 0;

    private bool canMove = true;

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        // Auto-find camera if not assigned
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // Ensure camera is a child of the player for tracking
        if (playerCamera != null && playerCamera.transform.parent != transform)
        {
            playerCamera.transform.SetParent(transform);
            playerCamera.transform.localPosition = new Vector3(0, 1f, 0); // Standard eye height
            playerCamera.transform.localRotation = Quaternion.identity;
        }

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // We are grounded, so recalculate move direction based on axes
        bool isRunning = Input.GetKey(runKey);
        float inputX = Input.GetAxis("Vertical");
        float inputY = Input.GetAxis("Horizontal");

        // Calculate movement direction
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        // Press Left Shift to run
        float curSpeedX = canMove ? (isRunning ? runSpeed : walkSpeed) * inputX : 0;
        float curSpeedY = canMove ? (isRunning ? runSpeed : walkSpeed) * inputY : 0;
        
        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * curSpeedX) + (right * curSpeedY);

        if (Input.GetButton("Jump") && canMove && characterController.isGrounded)
        {
            moveDirection.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }

        // Apply gravity. Gravity is multiplied by deltaTime twice (once here, and once when moveDirection is multiplied by deltaTime). This is because gravity should be applied continuously.
        if (!characterController.isGrounded)
        {
            moveDirection.y += gravity * Time.deltaTime;
        }

        // Move the controller
        characterController.Move(moveDirection * Time.deltaTime);

        // Player and Camera rotation
        if (canMove && playerCamera != null)
        {
            // Camera Pitch (Up/Down)
            rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            
            // Player Yaw (Left/Right) - Rotates the entire character
            transform.Rotate(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
        }
    }
}