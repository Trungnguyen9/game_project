using UnityEngine;

/// <summary>
/// Điều khiển di chuyển, nhảy và xoay hướng cho nhân vật người chơi (Pixel Knight).
/// Tương thích hoàn hảo với Unity 6.3 LTS (sử dụng Rigidbody2D.linearVelocity).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Thông số Di chuyển")]
    [Tooltip("Tốc độ di chuyển ngang của hiệp sĩ")]
    [SerializeField] private float moveSpeed = 7f;
    [Tooltip("Lực nhảy")]
    [SerializeField] private float jumpForce = 12f;

    [Header("Kiểm tra Mặt đất (Ground Check)")]
    [Tooltip("Vị trí chân nhân vật dùng để kiểm tra chạm đất")]
    [SerializeField] private Transform groundCheckPoint;
    [Tooltip("Bán kính vùng kiểm tra mặt đất")]
    [SerializeField] private float groundCheckRadius = 0.2f;
    [Tooltip("Layer được tính là mặt đất")]
    [SerializeField] private LayerMask groundLayer;

    // Component nội bộ
    private Rigidbody2D rb;
    private Animator anim;
    private float horizontalInput;
    private bool isGrounded;
    private bool isFacingRight = true;

    // Caching Animator parameter hashes để tránh cấp phát và hash chuỗi liên tục mỗi frame
    private static readonly int SpeedAnimHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedAnimHash = Animator.StringToHash("IsGrounded");

    // Public properties cho Animation hoặc hệ thống khác đọc trạng thái
    public bool IsGrounded => isGrounded;
    public float HorizontalVelocity => Mathf.Abs(rb.linearVelocity.x);
    public bool IsFacingRight => isFacingRight;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    private void Update()
    {
        // 1. Đọc tín hiệu điều khiển (A/D hoặc phím mũi tên)
        horizontalInput = ReadHorizontalInput();

        // 2. Kiểm tra mặt đất
        CheckGround();

        // 3. Xử lý nhảy (Phím Space hoặc W / Mũi tên lên)
        if (ReadJumpInputDown() && isGrounded)
        {
            Jump();
        }

        // 4. Tự động xoay hướng hiệp sĩ theo hướng di chuyển
        UpdateDirection();

        // 5. Cập nhật Animation tối ưu bằng int Hash
        if (anim != null)
        {
            anim.SetFloat(SpeedAnimHash, Mathf.Abs(horizontalInput));
            anim.SetBool(IsGroundedAnimHash, isGrounded);
        }
    }

    private void FixedUpdate()
    {
        // Di chuyển nhân vật dựa vào vật lý Rigidbody2D trong Unity 6
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);
    }

    /// <summary>
    /// Thực hiện cú nhảy khi đứng trên mặt đất.
    /// </summary>
    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    /// <summary>
    /// Kiểm tra xem nhân vật có đang đứng trên bề mặt layer Ground không.
    /// </summary>
    private void CheckGround()
    {
        if (groundCheckPoint != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);
        }
        else
        {
            // Dự phòng nếu chưa gán groundCheckPoint: kiểm tra ngay dưới vị trí nhân vật
            isGrounded = Physics2D.Raycast(transform.position, Vector2.down, 1.1f, groundLayer);
        }
    }

    /// <summary>
    /// Xoay lật nhân vật (flip scale) khi đổi hướng di chuyển.
    /// </summary>
    private void UpdateDirection()
    {
        if (horizontalInput > 0.01f && !isFacingRight)
        {
            Flip();
        }
        else if (horizontalInput < -0.01f && isFacingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 localScale = transform.localScale;
        localScale.x *= -1;
        transform.localScale = localScale;
    }

    // Biến lưu trạng thái hỗ trợ Legacy Input để tránh try/catch trong Update loop
    private static bool canUseLegacyInput = true;

    /// <summary>
    /// Đọc input ngang đa nền tảng, tương thích cả New Input System và Legacy Input.
    /// </summary>
    private float ReadHorizontalInput()
    {
        float input = 0f;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input += 1f;
        }
#endif

        if (input == 0f && canUseLegacyInput)
        {
            try
            {
                input = Input.GetAxisRaw("Horizontal");
            }
            catch
            {
                canUseLegacyInput = false;
            }
        }

        return input;
    }

    /// <summary>
    /// Đọc phím nhảy (Space).
    /// </summary>
    private bool ReadJumpInputDown()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                return true;
        }
#endif

        if (canUseLegacyInput)
        {
            try
            {
                return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
            }
            catch
            {
                canUseLegacyInput = false;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        // Vẽ vòng tròn hỗ trợ canh chỉnh GroundCheck trong Editor
        if (groundCheckPoint != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }
}
