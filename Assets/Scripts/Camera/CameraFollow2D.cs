using UnityEngine;

/// <summary>
/// Camera theo dõi nhân vật mượt mà trong không gian 2D Platformer.
/// Tự động tìm nhân vật theo Tag Player nếu chưa gán thủ công.
/// </summary>
public class CameraFollow2D : MonoBehaviour
{
    [Header("Mục tiêu")]
    [Tooltip("Mục tiêu theo dõi (thường là Player)")]
    [SerializeField] private Transform target;

    [Header("Độ mượt")]
    [Tooltip("Độ trễ mượt mà khi di chuyển camera")]
    [SerializeField] private float smoothTime = 0.25f;
    [Tooltip("Khoảng cách lệch vị trí (Offset)")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, -10f);

    [Header("Giới hạn Camera (Clamping)")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private float minX = -10f;
    [SerializeField] private float maxX = 50f;
    [SerializeField] private float minY = -2f;
    [SerializeField] private float maxY = 15f;

    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 lastTargetPos;
    private bool hasValidTarget = false;

    private void Start()
    {
        FindTarget();
    }

    private void FindTarget()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target != null)
        {
            lastTargetPos = target.position;
            hasValidTarget = true;
        }
    }

    private void LateUpdate()
    {
        if (target != null && target.gameObject.activeInHierarchy)
        {
            lastTargetPos = target.position;
            hasValidTarget = true;
        }
        else if (!hasValidTarget)
        {
            // Chỉ thử tìm lại 1 lần nếu chưa bao giờ có target
            FindTarget();
            if (!hasValidTarget) return;
        }

        Vector3 targetPos = lastTargetPos + offset;

        if (useBounds)
        {
            targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
            targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);
        }

        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, smoothTime);
    }
}
