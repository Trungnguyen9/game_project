using UnityEngine;

/// <summary>
/// Chướng ngại vật (chông gai, bẫy, dung nham hoặc hố sâu).
/// Gây sát thương khi đối tượng (Player) chạm vào.
/// </summary>
public class Hazard : MonoBehaviour
{
    [Header("Sát thương Bẫy")]
    [Tooltip("Lượng sát thương gây ra khi chạm bẫy")]
    [SerializeField] private int hazardDamage = 20;

    [Tooltip("Lực hất lùi khi đạp trúng chông")]
    [SerializeField] private float bounceForce = 8f;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            ApplyHazard(collision.gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            ApplyHazard(collision.gameObject);
        }
    }

    private void ApplyHazard(GameObject target)
    {
        Health targetHealth = target.GetComponent<Health>();
        if (targetHealth != null && targetHealth.IsPlayer)
        {
            targetHealth.TakeDamage(hazardDamage);

            // Tạo lực nảy lên khi đạp phải gai
            Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, bounceForce);
            }
        }
    }
}
