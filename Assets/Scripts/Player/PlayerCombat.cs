using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Quản lý hệ thống tấn công cận chiến của hiệp sĩ bằng kiếm (Phím J).
/// Sử dụng AttackPoint và OverlapCircle để gây sát thương chính xác lên quái vật (layer Enemy).
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Cấu hình Tấn công")]
    [Tooltip("Điểm xuất phát của đòn đánh kiếm")]
    [SerializeField] private Transform attackPoint;
    [Tooltip("Bán kính vùng sát thương của kiếm")]
    [SerializeField] private float attackRange = 0.8f;
    [Tooltip("Lượng sát thương mỗi nhát chém")]
    [SerializeField] private int attackDamage = 25;
    [Tooltip("Thời gian hồi giữa 2 đòn đánh (giây)")]
    [SerializeField] private float attackCooldown = 0.35f;
    [Tooltip("Layer chứa quái vật")]
    [SerializeField] private LayerMask enemyLayers;

    [Header("Hiệu ứng Trực quan & Lực đánh")]
    [Tooltip("Đối tượng vệt chém kiếm (Slash effect) hiển thị chớp nhoáng khi đánh")]
    [SerializeField] private GameObject slashVisualEffect;
    [Tooltip("Chuỗi khung hình của vệt chém mới (slash.png)")]
    [SerializeField] private Sprite[] slashFrames;
    [Tooltip("Thời gian hiển thị vệt kiếm (giây)")]
    [SerializeField] private float slashDuration = 0.22f;
    [Tooltip("Lực hất lùi quái vật khi đánh trúng")]
    [SerializeField] private float knockbackForce = 6.5f;

    // Trạng thái nội bộ
    private float nextAttackTime = 0f;
    private Animator anim;
    private SpriteRenderer slashRenderer;
    private Coroutine slashCoroutine;

    // Bộ nhớ đệm không cấp phát GC (Zero GC Allocations)
    private static readonly int AttackAnimHash = Animator.StringToHash("Attack");
    private readonly Collider2D[] hitCollidersBuffer = new Collider2D[16];
    private readonly HashSet<Health> damagedTargetsBuffer = new HashSet<Health>();
    private static bool canUseLegacyInput = true;

    public bool IsAttacking => Time.time < nextAttackTime;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        if (slashVisualEffect == null)
        {
            Transform slashChild = transform.Find("SlashEffect");
            if (slashChild != null) slashVisualEffect = slashChild.gameObject;
        }

        if (slashVisualEffect != null)
        {
            slashRenderer = slashVisualEffect.GetComponent<SpriteRenderer>();
        }
    }

    private void Start()
    {
        if (slashVisualEffect != null)
        {
            slashVisualEffect.SetActive(false);
        }

#if UNITY_EDITOR
        // Tự động nạp danh sách frame từ slash.png nếu chưa được gán
        if ((slashFrames == null || slashFrames.Length == 0) && slashVisualEffect != null)
        {
            var loaded = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Player/slash.png")
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();
            if (loaded != null && loaded.Length > 0)
            {
                slashFrames = loaded;
                if (slashRenderer != null && slashRenderer.sprite == null)
                {
                    slashRenderer.sprite = slashFrames[0];
                }
            }
        }
#endif
    }

    private void Update()
    {
        // Kiểm tra phím J và thời gian hồi chiêu
        if (Time.time >= nextAttackTime)
        {
            if (ReadAttackInputDown())
            {
                Attack();
                nextAttackTime = Time.time + attackCooldown;
            }
        }
    }

    /// <summary>
    /// Thực hiện đòn chém kiếm với hiệu năng tối ưu (Zero-alloc overlap).
    /// </summary>
    private void Attack()
    {
        // 0. Kích hoạt trigger animation chém tối ưu bằng Hash
        if (anim != null)
        {
            anim.SetTrigger(AttackAnimHash);
        }

        // 1. Kích hoạt hiệu ứng hình ảnh vệt chém nếu có
        if (slashVisualEffect != null)
        {
            if (slashCoroutine != null)
            {
                StopCoroutine(slashCoroutine);
            }
            slashCoroutine = StartCoroutine(ShowSlashRoutine());
        }

        // 2. Tìm điểm tấn công
        Vector3 origin = attackPoint != null ? attackPoint.position : transform.position;

        // 3. Quét các Collider thuộc layer Enemy bằng NonAlloc để không sinh GC rác
        int hitCount = Physics2D.OverlapCircleNonAlloc(origin, attackRange, hitCollidersBuffer, enemyLayers);

        // 4. Sử dụng bộ đệm HashSet tái sử dụng để đảm bảo mỗi đối tượng chỉ nhận sát thương đúng 1 lần
        damagedTargetsBuffer.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D enemyCollider = hitCollidersBuffer[i];
            if (enemyCollider == null) continue;

            Health targetHealth = enemyCollider.GetComponent<Health>() ?? enemyCollider.GetComponentInParent<Health>();

            if (targetHealth != null && damagedTargetsBuffer.Add(targetHealth))
            {
                targetHealth.TakeDamage(attackDamage);

                // Hiệu ứng quái vật bật lui lại khi bị đánh trúng (Knockback)
                EnemyAI enemyAI = enemyCollider.GetComponent<EnemyAI>() ?? enemyCollider.GetComponentInParent<EnemyAI>();
                if (enemyAI != null)
                {
                    float knockbackDir = Mathf.Sign(enemyCollider.transform.position.x - transform.position.x);
                    if (knockbackDir == 0) knockbackDir = transform.localScale.x > 0 ? 1 : -1;
                    enemyAI.ApplyKnockback(new Vector2(knockbackDir, 1f), knockbackForce);
                }
            }
        }
    }

    /// <summary>
    /// Hiển thị vệt chém hoạt họa mượt mà qua các khung hình của slash.png rồi ẩn đi.
    /// </summary>
    private IEnumerator ShowSlashRoutine()
    {
        if (slashVisualEffect == null) yield break;

        if (slashRenderer == null)
        {
            slashRenderer = slashVisualEffect.GetComponent<SpriteRenderer>();
        }

        slashVisualEffect.SetActive(true);

        if (slashFrames != null && slashFrames.Length > 0 && slashRenderer != null)
        {
            float frameDelay = slashDuration / slashFrames.Length;
            WaitForSeconds waitFrame = new WaitForSeconds(frameDelay);
            for (int i = 0; i < slashFrames.Length; i++)
            {
                if (slashFrames[i] != null)
                {
                    slashRenderer.sprite = slashFrames[i];
                }
                yield return waitFrame;
            }
        }
        else
        {
            yield return new WaitForSeconds(slashDuration);
        }

        slashVisualEffect.SetActive(false);
        slashCoroutine = null;
    }

    /// <summary>
    /// Đọc phím tấn công (J hoặc Chuột trái).
    /// </summary>
    private bool ReadAttackInputDown()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb.jKey.wasPressedThisFrame) return true;
        }
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame) return true;
        }
#endif

        if (canUseLegacyInput)
        {
            try
            {
                return Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0);
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
        // Vẽ vùng tấn công trong Scene view để tiện canh chỉnh tầm đánh
        Vector3 origin = attackPoint != null ? attackPoint.position : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, attackRange);
    }
}
