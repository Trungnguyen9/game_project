using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý toàn bộ giao diện người dùng (UI):
/// - Thanh máu và chỉ số máu của hiệp sĩ.
/// - Đếm số lượng quái vật còn lại.
/// - Bảng Game Over (Thua).
/// - Bảng Victory (Thắng).
/// - Bảng Pause (Tạm dừng).
/// Hỗ trợ cả UI Text chuẩn và TextMeshPro.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Giao diện Máu Hiệp sĩ (HUD)")]
    [Tooltip("Thanh trượt hiển thị máu")]
    [SerializeField] private Slider healthSlider;
    [Tooltip("Chữ hiển thị số máu (Text chuẩn)")]
    [SerializeField] private Text healthLegacyText;
    [Tooltip("Chữ hiển thị số máu (TextMeshPro)")]
    [SerializeField] private TextMeshProUGUI healthTMPText;

    [Header("Giao diện Quái vật (HUD)")]
    [Tooltip("Chữ hiển thị số lượng quái vật (Text chuẩn)")]
    [SerializeField] private Text monsterCounterLegacyText;
    [Tooltip("Chữ hiển thị số lượng quái vật (TextMeshPro)")]
    [SerializeField] private TextMeshProUGUI monsterCounterTMPText;

    [Header("Bảng Game Over (Thua cuộc)")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button gameOverRestartButton;
    [SerializeField] private Button gameOverQuitButton;

    [Header("Bảng Victory (Chiến thắng)")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private Button victoryRestartButton;
    [SerializeField] private Button victoryQuitButton;

    [Header("Bảng Pause (Tạm dừng)")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button pauseResumeButton;
    [SerializeField] private Button pauseRestartButton;
    [SerializeField] private Button pauseQuitButton;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Đảm bảo các bảng thông báo ẩn lúc bắt đầu
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        // Gán sự kiện cho các nút bấm
        SetupButtons();
    }

    private Health trackedPlayerHealth;

    private void Start()
    {
        // Tự động tìm Player và đăng ký sự kiện máu
        HookPlayerHealth();
    }

    private void OnDestroy()
    {
        if (trackedPlayerHealth != null)
        {
            trackedPlayerHealth.OnHealthChanged -= UpdateHealthBar;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Gán listener cho toàn bộ các nút bấm trong UI.
    /// </summary>
    private void SetupButtons()
    {
        // Nút trong Game Over
        if (gameOverRestartButton != null)
            gameOverRestartButton.onClick.AddListener(() => GameManager.Instance.RestartGame());
        if (gameOverQuitButton != null)
            gameOverQuitButton.onClick.AddListener(() => GameManager.Instance.QuitGame());

        // Nút trong Victory
        if (victoryRestartButton != null)
            victoryRestartButton.onClick.AddListener(() => GameManager.Instance.RestartGame());
        if (victoryQuitButton != null)
            victoryQuitButton.onClick.AddListener(() => GameManager.Instance.QuitGame());

        // Nút trong Pause
        if (pauseResumeButton != null)
            pauseResumeButton.onClick.AddListener(() => GameManager.Instance.ResumeGame());
        if (pauseRestartButton != null)
            pauseRestartButton.onClick.AddListener(() => GameManager.Instance.RestartGame());
        if (pauseQuitButton != null)
            pauseQuitButton.onClick.AddListener(() => GameManager.Instance.QuitGame());
    }

    /// <summary>
    /// Tìm hiệp sĩ trong scene và lắng nghe sự kiện thay đổi máu.
    /// </summary>
    public void HookPlayerHealth()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            if (trackedPlayerHealth != null)
            {
                trackedPlayerHealth.OnHealthChanged -= UpdateHealthBar;
            }

            trackedPlayerHealth = player.GetComponent<Health>();
            if (trackedPlayerHealth != null)
            {
                trackedPlayerHealth.OnHealthChanged += UpdateHealthBar;
                // Cập nhật trạng thái máu ban đầu
                UpdateHealthBar(trackedPlayerHealth.CurrentHealth, trackedPlayerHealth.MaxHealth);
            }
        }
    }

    /// <summary>
    /// Cập nhật thanh máu và chữ số hiển thị.
    /// </summary>
    public void UpdateHealthBar(int currentHealth, int maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        string textContent = $"{currentHealth} / {maxHealth}";

        if (healthLegacyText != null)
        {
            healthLegacyText.text = textContent;
        }
        if (healthTMPText != null)
        {
            healthTMPText.text = textContent;
        }
    }

    /// <summary>
    /// Cập nhật hiển thị số lượng quái vật đã tiêu diệt / tổng số quái vật (đi kèm logo đầu lâu).
    /// </summary>
    public void UpdateMonsterCounter(int remaining, int total)
    {
        int killed = Mathf.Max(0, total - remaining);
        string textContent = $"x {killed} / {total}";

        if (monsterCounterLegacyText != null)
        {
            monsterCounterLegacyText.text = textContent;
        }
        if (monsterCounterTMPText != null)
        {
            monsterCounterTMPText.text = textContent;
        }
    }

    /// <summary>
    /// Hiển thị màn hình Thua cuộc (Game Over).
    /// </summary>
    public void ShowGameOverScreen()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Hiển thị màn hình Chiến thắng (Victory).
    /// </summary>
    public void ShowVictoryScreen()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Bật/Tắt màn hình Tạm dừng (Pause).
    /// </summary>
    public void ShowPauseScreen(bool isPaused)
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(isPaused);
        }
    }
}
