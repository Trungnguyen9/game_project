using UnityEngine;

/// <summary>
/// Trí thông minh nhân tạo (AI) cơ bản cho Quái vật:
/// 1. Tuần tra (Patrol) qua lại giữa 2 điểm.
/// 2. Phát hiện (Detect) người chơi khi vào phạm vi.
/// 3. Đuổi theo (Chase) người chơi.
/// 4. Tấn công (Attack) cận chiến với thời gian hồi chiêu khi tiếp cận người chơi.
/// Sử dụng hoàn toàn logic C# và Rigidbody2D đơn giản, không cần NavMesh.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class EnemyAI : MonoBehaviour
{
    private enum EnemyState
    {
        Patrol,
        Chase,
        Attack
    }

    [Header("Thiết lập Tuần tra (Patrol)")]
    [Tooltip("Tốc độ di chuyển khi tuần tra")]
    [SerializeField] private float patrolSpeed = 2f;
    [Tooltip("Điểm tuần tra A (Tùy chọn, nếu để trống sẽ tự lấy khoảng cách tính từ điểm sinh ra)")]
    [SerializeField] private Transform patrolPointA;
    [Tooltip("Điểm tuần tra B (Tùy chọn)")]
    [SerializeField] private Transform patrolPointB;
    [Tooltip("Bán kính tuần tra mặc định nếu không đặt điểm cụ thể")]
    [SerializeField] private float defaultPatrolDistance = 3f;

    [Header("Thiết lập Phát hiện & Rượt đuổi (Chase)")]
    [Tooltip("Bán kính phát hiện hiệp sĩ")]
    [SerializeField] private float detectionRange = 5f;
    [Tooltip("Tốc độ di chuyển khi đuổi theo")]
    [SerializeField] private float chaseSpeed = 3.5f;

    [Header("Thiết lập Tấn công (Combat)")]
    [Tooltip("Sát thương gây ra cho người chơi mỗi đòn")]
    [SerializeField] private int attackDamage = 15;
    [Tooltip("Khoảng cách tiếp cận để ra đòn")]
    [SerializeField] private float attackRange = 1.2f;
    [Tooltip("Thời gian hồi giữa 2 lần tấn công (giây)")]
    [SerializeField] private float attackCooldown = 1.0f;

    // Thành phần & Tọa độ
    private Rigidbody2D rb;
    private Health health;
    private Animator anim;
    private Transform playerTransform;
    private Health playerHealth;

    private EnemyState currentState = EnemyState.Patrol;
    private Vector2 targetPatrolPosition;
    private Vector2 leftLimit;
    private Vector2 rightLimit;
    private float nextAttackTime = 0f;
    // Sprite gốc vẽ quái vật nhìn sang bên TRÁI, do đó mặc định ban đầu là false
    private bool isFacingRight = false;
    private bool isKnockedBack = false;
    private float knockbackTimer = 0f;
    private float targetHorizontalVelocity = 0f;

    // Caching Animator parameter hashes
    private static readonly int IsMovingAnimHash = Animator.StringToHash("IsMoving");
    private static readonly int AttackAnimHash = Animator.StringToHash("Attack");

    // Cache bình phương tầm xa để tối ưu không cần tính căn bậc 2 (Mathf.Sqrt)
    private float sqrAttackRange;
    private float sqrDetectionRange;

    /// <summary>
    /// Kích hoạt hiệu ứng quái vật bị hất lùi (Knockback) khi bị Player đánh trúng.
    /// </summary>
    public void ApplyKnockback(Vector2 direction, float force, float duration = 0.25f)
    {
        if (health != null && health.IsDead) return;

        isKnockedBack = true;
        knockbackTimer = duration;

        // Reset vận tốc hiện tại và tác dụng lực hất văng lùi lại
        rb.linearVelocity = Vector2.zero;
        Vector2 knockbackVector = new Vector2(direction.x * force, Mathf.Max(force * 0.35f, 2.5f));
        rb.AddForce(knockbackVector, ForceMode2D.Impulse);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        anim = GetComponent<Animator>();

        sqrAttackRange = attackRange * attackRange;
        sqrDetectionRange = detectionRange * detectionRange;
    }

    private void Start()
    {
        // 1. Khởi tạo điểm tuần tra
        Vector2 startPos = transform.position;
        if (patrolPointA != null && patrolPointB != null)
        {
            leftLimit = patrolPointA.position.x < patrolPointB.position.x ? patrolPointA.position : patrolPointB.position;
            rightLimit = patrolPointA.position.x < patrolPointB.position.x ? patrolPointB.position : patrolPointA.position;
        }
        else
        {
            leftLimit = new Vector2(startPos.x - defaultPatrolDistance, startPos.y);
            rightLimit = new Vector2(startPos.x + defaultPatrolDistance, startPos.y);
        }
        targetPatrolPosition = rightLimit;

        // Đồng bộ hướng nhìn ban đầu dựa trên scale
        isFacingRight = transform.localScale.x < 0;

        // 2. Tìm kiếm Player trong màn chơi
        FindPlayer();
    }

    private void Update()
    {
        if (health.IsDead)
        {
            targetHorizontalVelocity = 0f;
            return;
        }

        // Nếu đang trong thời gian bị hất lùi, tạm thời giữ quán tính vật lý và bỏ qua cập nhật di chuyển
        if (isKnockedBack)
        {
            knockbackTimer -= Time.deltaTime;
            if (knockbackTimer <= 0f)
            {
                isKnockedBack = false;
            }
            return;
        }

        // Nếu Player chết, chuyển sang trạng thái tuần tra mà không tìm lại liên tục
        if (playerHealth != null && playerHealth.IsDead)
        {
            currentState = EnemyState.Patrol;
        }
        else if (playerTransform == null)
        {
            // Chỉ tìm kiếm lại khi chưa từng tìm thấy (vd player spawn sau)
            FindPlayer();
            currentState = EnemyState.Patrol;
        }

        // Đánh giá trạng thái
        EvaluateState();

        // Xử lý hành vi theo trạng thái
        switch (currentState)
        {
            case EnemyState.Patrol:
                HandlePatrol();
                break;
            case EnemyState.Chase:
                HandleChase();
                break;
            case EnemyState.Attack:
                HandleAttack();
                break;
        }

        // Cập nhật Animation trạng thái di chuyển bằng int Hash
        if (anim != null)
        {
            anim.SetBool(IsMovingAnimHash, currentState == EnemyState.Patrol || currentState == EnemyState.Chase);
        }
    }

    private void FixedUpdate()
    {
        if (health.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Chỉ tác động vận tốc khi không bị knockback
        if (!isKnockedBack)
        {
            rb.linearVelocity = new Vector2(targetHorizontalVelocity, rb.linearVelocity.y);
        }
    }

    /// <summary>
    /// Đánh giá và chuyển đổi trạng thái dựa trên khoảng cách bình phương (sqrMagnitude).
    /// </summary>
    private void EvaluateState()
    {
        if (playerTransform == null || playerHealth == null || playerHealth.IsDead)
        {
            currentState = EnemyState.Patrol;
            return;
        }

        float sqrDistToPlayer = ((Vector2)transform.position - (Vector2)playerTransform.position).sqrMagnitude;

        if (sqrDistToPlayer <= sqrAttackRange)
        {
            currentState = EnemyState.Attack;
        }
        else if (sqrDistToPlayer <= sqrDetectionRange)
        {
            currentState = EnemyState.Chase;
        }
        else
        {
            currentState = EnemyState.Patrol;
        }
    }

    /// <summary>
    /// Hành vi tuần tra qua lại giữa hai mốc tọa độ.
    /// </summary>
    private void HandlePatrol()
    {
        float direction = Mathf.Sign(targetPatrolPosition.x - transform.position.x);
        targetHorizontalVelocity = direction * patrolSpeed;

        // Kiểm tra xem đã đến gần điểm mốc chưa
        if (Mathf.Abs(transform.position.x - targetPatrolPosition.x) < 0.3f)
        {
            targetPatrolPosition = (targetPatrolPosition == rightLimit) ? leftLimit : rightLimit;
        }

        UpdateFacing(direction);
    }

    /// <summary>
    /// Hành vi đuổi theo hướng của người chơi.
    /// </summary>
    private void HandleChase()
    {
        if (playerTransform == null) return;

        float direction = Mathf.Sign(playerTransform.position.x - transform.position.x);
        targetHorizontalVelocity = direction * chaseSpeed;

        UpdateFacing(direction);
    }

    /// <summary>
    /// Hành vi dừng lại và ra đòn khi ở gần người chơi.
    /// </summary>
    private void HandleAttack()
    {
        // Dừng di chuyển ngang khi tấn công
        targetHorizontalVelocity = 0f;

        if (playerTransform != null)
        {
            float direction = Mathf.Sign(playerTransform.position.x - transform.position.x);
            UpdateFacing(direction);
        }

        // Tấn công nếu đã qua thời gian hồi chiêu
        if (Time.time >= nextAttackTime)
        {
            if (anim != null)
            {
                anim.SetTrigger(AttackAnimHash);
            }

            if (playerHealth != null && !playerHealth.IsDead)
            {
                playerHealth.TakeDamage(attackDamage);
            }
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    /// <summary>
    /// Cập nhật xoay hướng mặt quái vật theo hướng di chuyển.
    /// </summary>
    private void UpdateFacing(float direction)
    {
        if (direction > 0.05f && !isFacingRight)
        {
            Flip();
        }
        else if (direction < -0.05f && isFacingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        // Sprite gốc (scale.x > 0) nhìn sang TRÁI. Khi quay sang PHẢI cần scale.x âm (-1 * abs).
        scale.x = (isFacingRight ? -1f : 1f) * Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    /// <summary>
    /// Tìm tham chiếu tới người chơi trong Scene.
    /// </summary>
    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerHealth = playerObj.GetComponent<Health>();
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Vùng phát hiện
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Vùng tấn công
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Đường tuần tra
        Gizmos.color = Color.cyan;
        if (Application.isPlaying)
        {
            Gizmos.DrawLine(new Vector3(leftLimit.x, transform.position.y, 0), new Vector3(rightLimit.x, transform.position.y, 0));
        }
        else
        {
            Vector3 center = transform.position;
            Gizmos.DrawLine(new Vector3(center.x - defaultPatrolDistance, center.y, 0), new Vector3(center.x + defaultPatrolDistance, center.y, 0));
        }
    }
}
