using UnityEngine;
using UnityEngine.InputSystem;

// =============================================================================
// PlayerController.cs
// =============================================================================
// PURPOSE
//   First-person player movement and camera control. This is the "body" of the
//   player - it handles WASD movement, mouse look, sprint, gravity, and lets
//   other systems disable input (e.g., when paused, dead, or hiding in a chest).
//
// HOW IT FITS IN THE ARCHITECTURE
//   - Sits on the Player GameObject (tagged "Player")
//   - Sibling components: PlayerVisibility (hide state), PlayerInteraction (E to interact)
//   - Child object: a Camera at head height (~1.6u). The camera is rotated for
//     pitch (look up/down); the body is rotated for yaw (look left/right). This
//     is the standard FPS rig.
//   - Input comes from Unity's New Input System via PlayerInput component on the
//     same GameObject. The PlayerInput component is configured with our actions
//     asset (Assets/InputSystem_Actions.inputactions) and dispatches to the
//     OnMove / OnLook / OnSprint methods below using "Send Messages" or
//     "Invoke Unity Events" mode.
//
// REQUIREMENTS
//   - CharacterController component on this GameObject (auto-required)
//   - PlayerInput component pointing at our actions asset
//   - Child Camera object at head height
//   - GameObject tag = "Player" (so other systems like EnemyDetection find it)
// =============================================================================

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // ------------------------- Inspector fields ------------------------------
    // [Header] groups fields in the Inspector for readability.
    // [SerializeField] private = visible in Inspector but not part of public API.
    // Designers (Nick) can tune these per-prefab without touching code.

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3.5f;     // Default walk speed in units/second
    [SerializeField] private float sprintSpeed = 5.5f;   // Speed while holding Shift
    [SerializeField] private float gravity = -9.81f;     // Negative = pulls down

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2f; // Multiplier on raw mouse delta
    [SerializeField] private float maxLookAngle = 80f;    // Pitch clamp - prevents flipping upside down

    [Header("References")]
    [SerializeField] private Transform cameraHolder;      // Assign Camera transform; auto-found if null

    // ------------------------- Runtime state ---------------------------------
    private CharacterController controller;   // Unity's collide-and-slide capsule controller
    private Vector2 moveInput;                // (-1..1, -1..1) from WASD/stick
    private Vector2 lookInput;                // Raw mouse/stick delta this frame
    private float verticalVelocity;           // Tracks gravity accumulation
    private float xRotation;                  // Current camera pitch (up/down angle)
    private bool isSprinting;                 // True while sprint button is held
    private bool inputEnabled = true;         // Master toggle - false during pause/cutscene/death/hide

    // ------------------------- Public API ------------------------------------
    // Other systems read these. Keeping them as expression-bodied properties
    // avoids accidental external mutation.

    // True only when sprint button held AND actively moving (matters for
    // EnemyDetection's "sprint noise" check - sprinting in place is silent).
    public bool IsSprinting => isSprinting && moveInput.sqrMagnitude > 0.01f;

    // Current world-space velocity. DarkZone reads this to check stand-still.
    public Vector3 Velocity => controller.velocity;

    // ------------------------- Unity lifecycle -------------------------------

    private void Awake()
    {
        // Cache the CharacterController reference once. GetComponent is cheap
        // but doing it every frame in Update is wasteful.
        controller = GetComponent<CharacterController>();

        // Fallback: if the designer forgot to assign cameraHolder in the
        // Inspector, find the first child Camera and use its transform.
        if (cameraHolder == null)
            cameraHolder = GetComponentInChildren<Camera>().transform;
    }

    private void Start()
    {
        // Lock and hide the cursor so mouse look feels natural. ESC unlocks it
        // (handled by GameManager when entering Paused state).
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // Early-out when input is disabled. This is the single place we gate
        // movement; SetInputEnabled() also clears stale input so the player
        // doesn't drift after re-enabling.
        if (!inputEnabled) return;

        HandleMouseLook();
        HandleMovement();
    }

    // ------------------------- Camera (mouse look) ---------------------------
    // Standard FPS look:
    //   - Mouse X (horizontal) rotates the BODY around world up (yaw)
    //   - Mouse Y (vertical) rotates the CAMERA only around its local right (pitch)
    // Why split? If you pitched the body, the player would tip over. Pitching
    // only the camera keeps the body upright and the CharacterController's
    // movement axes (transform.forward / transform.right) on the horizontal plane.
    private void HandleMouseLook()
    {
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        // Subtract because mouse-up gives positive Y, but Unity's X-rotation
        // is negative for "looking up" (right-hand rule).
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        // Pitch the camera only.
        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        // Yaw the body (and the camera, since it's a child).
        transform.Rotate(Vector3.up * mouseX);
    }

    // ------------------------- Movement & gravity ----------------------------
    private void HandleMovement()
    {
        // CharacterController-specific gravity trick: when grounded, snap
        // verticalVelocity to a small negative value so the controller stays
        // "stuck" to the floor on slopes / stairs and isGrounded stays true.
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        float speed = isSprinting ? sprintSpeed : walkSpeed;

        // Horizontal movement: combine input with body's forward/right vectors
        // so movement is relative to facing direction (W = forward, A = strafe left).
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * speed * Time.deltaTime);

        // Vertical movement (gravity). Done as a separate Move call so horizontal
        // speed doesn't compound with vertical fall speed.
        verticalVelocity += gravity * Time.deltaTime;
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    // ------------------------- External control ------------------------------
    // Called by GameManager (pause/death/victory) and HideableChest (entering
    // a chest). Clearing inputs prevents the player from "remembering" they
    // were holding W when control returns.
    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (!enabled)
        {
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
            isSprinting = false;
        }
    }

    // ------------------------- Input System callbacks ------------------------
    // These are wired up in the Inspector via PlayerInput's "Invoke Unity Events"
    // mode (or auto-bound via "Send Messages"). Make sure the action names in
    // InputSystem_Actions.inputactions match: "Move", "Look", "Sprint".
    //
    // TIP: If pressing keys does nothing, 90% of the time the cause is one of:
    //   1) PlayerInput component missing or pointing at a different actions asset
    //   2) Action map not enabled (set Default Map = "Player" or call .Enable())
    //   3) Behavior set to "Send Messages" but methods named differently than expected
    //   4) Method names below renamed without updating the Inspector binding

    public void OnMove(InputAction.CallbackContext context)
    {
        // ReadValue<Vector2>() works for both stick (continuous) and WASD (composite).
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        // performed = button just pressed (or held in continuous mode).
        // canceled = button released. Setting both lets us handle hold-to-sprint cleanly.
        isSprinting = context.performed;
        if (context.canceled) isSprinting = false;
    }
}
