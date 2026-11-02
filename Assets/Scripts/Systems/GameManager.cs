using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Quản lý vòng đời trò chơi: đếm quái vật, thắng, thua, tạm dừng, chơi lại và thoát game.
/// Áp dụng Singleton pattern đơn giản, dễ truy cập từ bất kỳ script nào.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        Playing,
        Paused,
        GameOver,
        Victory
    }

    [Header("Trạng thái Game")]
    [SerializeField] private GameState currentState = GameState.Playing;

    [Header("Thông tin Quái vật")]
    [SerializeField] private int totalEnemies = 0;
    [SerializeField] private int remainingEnemies = 0;

    public GameState CurrentState => currentState;
    public int TotalEnemies => totalEnemies;
    public int RemainingEnemies => remainingEnemies;
    public int DefeatedEnemies => totalEnemies - remainingEnemies;

    private void Awake()
    {
        // Khởi tạo Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Đảm bảo thời gian game chạy bình thường khi bắt đầu hoặc load lại scene
        Time.timeScale = 1f;
    }

    private void Start()
    {
        // Tự động đếm tất cả quái vật có trong Scene
        InitializeEnemies();
    }

    private void Update()
    {
        // Nhấn Escape để tạm dừng hoặc tiếp tục game
        if (ReadEscapeInputDown())
        {
            if (currentState == GameState.Playing)
            {
                PauseGame();
            }
            else if (currentState == GameState.Paused)
            {
                ResumeGame();
            }
        }
    }

    /// <summary>
    /// Đếm tổng số quái vật có trong màn chơi khi bắt đầu.
    /// </summary>
    public void InitializeEnemies()
    {
        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        totalEnemies = enemies.Length;
        remainingEnemies = totalEnemies;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateMonsterCounter(remainingEnemies, totalEnemies);
        }
    }

    /// <summary>
    /// Được gọi từ Health.cs khi một quái vật bị hạ gục.
    /// </summary>
    public void OnEnemyKilled(GameObject enemy)
    {
        if (currentState != GameState.Playing) return;

        remainingEnemies--;
        remainingEnemies = Mathf.Max(0, remainingEnemies);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateMonsterCounter(remainingEnemies, totalEnemies);
        }

        // Kiểm tra điều kiện Chiến thắng: tiêu diệt toàn bộ quái vật
        if (remainingEnemies <= 0)
        {
            TriggerVictory();
        }
    }

    /// <summary>
    /// Được gọi từ Health.cs khi người chơi hết máu.
    /// </summary>
    public void OnPlayerDeath()
    {
        if (currentState != GameState.Playing) return;

        currentState = GameState.GameOver;
        Time.timeScale = 0f; // Dừng thời gian game

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowGameOverScreen();
        }
    }

    /// <summary>
    /// Kích hoạt màn hình Chiến thắng.
    /// </summary>
    private void TriggerVictory()
    {
        currentState = GameState.Victory;
        Time.timeScale = 0f;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowVictoryScreen();
        }
    }

    /// <summary>
    /// Tạm dừng trò chơi.
    /// </summary>
    public void PauseGame()
    {
        if (currentState != GameState.Playing) return;

        currentState = GameState.Paused;
        Time.timeScale = 0f;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowPauseScreen(true);
        }
    }

    /// <summary>
    /// Tiếp tục chơi tiếp sau khi tạm dừng.
    /// </summary>
    public void ResumeGame()
    {
        if (currentState != GameState.Paused) return;

        currentState = GameState.Playing;
        Time.timeScale = 1f;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowPauseScreen(false);
        }
    }

    /// <summary>
    /// Chơi lại màn chơi hiện tại.
    /// </summary>
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Thoát khỏi trò chơi.
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Đọc phím Escape đa nền tảng.
    /// </summary>
    private bool ReadEscapeInputDown()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) return true;
        }
#endif

        try
        {
            return Input.GetKeyDown(KeyCode.Escape);
        }
        catch
        {
            return false;
        }
    }
}
