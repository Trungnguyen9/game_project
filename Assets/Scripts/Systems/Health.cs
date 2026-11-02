using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Quản lý lượng máu (HP), nhận sát thương, hồi máu và trạng thái bất tử (i-frames).
/// Dùng chung cho cả Người chơi (Player) và Quái vật (Enemy).
/// </summary>
public class Health : MonoBehaviour
{
    [Header("Thiết lập Máu")]
    [Tooltip("Lượng máu tối đa")]
    [SerializeField] private int maxHealth = 100;
    
    [Tooltip("Đánh dấu nếu đây là Người chơi (Player)")]
    [SerializeField] private bool isPlayer = false;

    [Header("Thời gian Bất tử (Chỉ dành cho Player hoặc Boss)")]
    [Tooltip("Thời gian bất tử sau khi nhận đòn (giây)")]
    [SerializeField] private float invulnerabilityDuration = 1.0f;
    [Tooltip("Tốc độ nhấp nháy khi bất tử (giây)")]
    [SerializeField] private float blinkInterval = 0.1f;

    [Header("Hiệu ứng khi dính đòn")]
    [Tooltip("Màu nhấp nháy khi bị trúng đòn")]
    [SerializeField] private Color hurtColor = Color.red;

    // Trạng thái nội bộ
    private int currentHealth;
    private bool isInvulnerable = false;
    private bool isDead = false;

    // Component tham chiếu & Caching
    private SpriteRenderer spriteRenderer;
    private Animator anim;
    private Collider2D col;
    private Rigidbody2D rb;
    private EnemyAI enemyAI;
    private Color originalColor;
    private Coroutine invulnerabilityCoroutine;
    private Coroutine flashCoroutine;

    // Tối ưu GC Alloc bằng cached WaitForSeconds & Animator hash
    private static readonly int DeathAnimHash = Animator.StringToHash("Death");
    private WaitForSeconds blinkWait;
    private static readonly WaitForSeconds QuickFlashWait = new WaitForSeconds(0.1f);

    // Events để UI hoặc hệ thống khác lắng nghe
    public event Action<int, int> OnHealthChanged; // (currentHealth, maxHealth)
    public event Action OnDeath;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public bool IsPlayer => isPlayer;

    private void Awake()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        enemyAI = GetComponent<EnemyAI>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        blinkWait = new WaitForSeconds(blinkInterval);
    }

    private void Start()
    {
        // Thông báo máu khởi đầu cho UI
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Nhận sát thương từ nguồn ngoài.
    /// </summary>
    /// <param name="damage">Lượng sát thương nhận vào</param>
    public void TakeDamage(int damage)
    {
        // Bỏ qua nếu đã chết hoặc đang trong trạng thái bất tử
        if (isDead || isInvulnerable) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // Kích hoạt sự kiện thay đổi máu
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Kiểm tra điều kiện tử vong
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Nếu còn sống và có thời gian bất tử, kích hoạt hiệu ứng bất tử + nhấp nháy
            if (invulnerabilityDuration > 0)
            {
                if (invulnerabilityCoroutine != null)
                {
                    StopCoroutine(invulnerabilityCoroutine);
                }
                invulnerabilityCoroutine = StartCoroutine(InvulnerabilityRoutine());
            }
            else
            {
                // Quái vật không có i-frames vẫn chớp đỏ một chút để tạo cảm giác trúng đòn
                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                flashCoroutine = StartCoroutine(QuickFlashRoutine());
            }
        }
    }

    /// <summary>
    /// Hồi máu cho đối tượng.
    /// </summary>
    public void Heal(int amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Xử lý khi máu về 0.
    /// </summary>
    private void Die()
    {
        if (isDead) return;
        isDead = true;

        OnDeath?.Invoke();

        if (isPlayer)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerDeath();
            }
            gameObject.SetActive(false);
        }
        else
        {
            // Kích hoạt animation chết tối ưu bằng int Hash
            if (anim != null)
            {
                anim.SetTrigger(DeathAnimHash);
            }

            // Vô hiệu hóa va chạm và AI để quái không gây sát thương khi đang gục ngã
            if (col != null) col.enabled = false;
            if (rb != null) rb.linearVelocity = Vector2.zero;
            if (enemyAI != null) enemyAI.enabled = false;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnEnemyKilled(gameObject);
            }

            // Biến mất khỏi màn chơi sau 0.6s khi hoạt ảnh chết hoàn tất
            Destroy(gameObject, 0.6f);
        }
    }

    /// <summary>
    /// Coroutine nhấp nháy trong khoảng thời gian bất tử (tối ưu zero-alloc).
    /// </summary>
    private IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;
        float elapsed = 0f;

        while (elapsed < invulnerabilityDuration)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.3f);
            }
            yield return blinkWait;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            yield return blinkWait;

            elapsed += blinkInterval * 2f;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        isInvulnerable = false;
        invulnerabilityCoroutine = null;
    }

    /// <summary>
    /// Coroutine chớp đỏ nhanh khi bị đánh trúng.
    /// </summary>
    private IEnumerator QuickFlashRoutine()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = hurtColor;
            yield return QuickFlashWait;
            spriteRenderer.color = originalColor;
        }
        flashCoroutine = null;
    }
}
