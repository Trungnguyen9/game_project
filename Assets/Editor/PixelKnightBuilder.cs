using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using System.IO;
using System.Linq;

[InitializeOnLoad]
public class PixelKnightAutoBuilder
{
    private const string BUILD_KEY = "PixelKnight_AutoBuild_Version_3_2";

    static PixelKnightAutoBuilder()
    {
        EditorApplication.delayCall += CheckAndBuild;
    }

    private static void CheckAndBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        string currentVersion = EditorPrefs.GetString(BUILD_KEY, "");
        if (currentVersion != "v3.2")
        {
            Debug.Log("[Pixel Knight] Auto-building updated game scene, prefabs, environment and UI...");
            EditorPrefs.SetString(BUILD_KEY, "v3.2");
            PixelKnightBuilder.BuildGameInternal(false);
        }
    }
}

/// <summary>
/// Script Editor tự động thiết lập toàn bộ Prefabs, UI, Hoạt ảnh (Animations), và Màn chơi MainScene cho game
/// "Pixel Knight: Monster Hunt" tương thích Unity 6.3 LTS.
/// Tự động tích hợp bộ ảnh người dùng cung cấp (Hiệp sĩ, Quái vật, Rừng cây).
/// </summary>
public class PixelKnightBuilder : EditorWindow
{
    [MenuItem("Pixel Knight/Build Entire Game (Scene + Prefabs)")]
    public static void BuildEntireGame()
    {
        BuildGameInternal(true);
    }

    public static void BuildGameInternal(bool showDialog)
    {
        if (showDialog) EditorUtility.DisplayProgressBar("Pixel Knight Builder", "Đang chuẩn bị thư mục...", 0.1f);
        EnsureDirectories();

        if (showDialog) EditorUtility.DisplayProgressBar("Pixel Knight Builder", "Đang tạo Hoạt ảnh (Animations)...", 0.25f);
        CreateAnimations(out RuntimeAnimatorController playerController, out RuntimeAnimatorController enemyController);

        if (showDialog) EditorUtility.DisplayProgressBar("Pixel Knight Builder", "Đang tạo Prefabs...", 0.45f);
        CreatePrefabs(playerController, enemyController, out GameObject playerPrefab, out GameObject enemyPrefab, out GameObject groundPrefab, out GameObject platformPrefab, out GameObject spikePrefab);

        if (showDialog) EditorUtility.DisplayProgressBar("Pixel Knight Builder", "Đang xây dựng MainScene...", 0.75f);
        BuildMainScene(playerPrefab, enemyPrefab, groundPrefab, platformPrefab, spikePrefab);

        if (showDialog)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Thành công!", "Đã cập nhật toàn bộ đồ họa, hoạt ảnh, Prefabs và MainScene từ ảnh của bạn! Bạn có thể nhấn Play để trải nghiệm ngay!", "OK");
        }
        else
        {
            Debug.Log("[Pixel Knight] MainScene and Prefabs built successfully with user sprites!");
        }
    }

    private static void EnsureDirectories()
    {
        string[] dirs = {
            "Assets/Animations",
            "Assets/Prefabs",
            "Assets/Scenes",
            "Assets/Sprites/Player",
            "Assets/Sprites/Enemies",
            "Assets/Sprites/Environment",
            "Assets/Sprites/UI"
        };
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        AssetDatabase.Refresh();
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite[] LoadSlicedSprites(string path)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        System.Array.Sort(sprites, (a, b) =>
        {
            int na = ExtractTrailingNumber(a.name);
            int nb = ExtractTrailingNumber(b.name);
            if (na != -1 && nb != -1) return na.CompareTo(nb);
            return string.Compare(a.name, b.name, System.StringComparison.Ordinal);
        });
        if (sprites.Length == 0)
        {
            Sprite single = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (single != null) return new Sprite[] { single };
        }
        return sprites;
    }

    private static Sprite FindSpriteByName(Sprite[] sprites, string name)
    {
        if (sprites == null) return null;
        return System.Array.Find(sprites, s => s != null && s.name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
    }

    private static int ExtractTrailingNumber(string str)
    {
        var match = System.Text.RegularExpressions.Regex.Match(str, @"\d+$");
        return match.Success ? int.Parse(match.Value) : -1;
    }

    private static AnimationClip CreateClip(string savePath, Sprite[] frames, float fps, bool loop)
    {
        AnimationClip clip = new AnimationClip();
        clip.frameRate = fps;

        if (frames != null && frames.Length > 0)
        {
            EditorCurveBinding binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

            ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i / fps,
                    value = frames[i]
                };
            }
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        }

        if (loop)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        AssetDatabase.CreateAsset(clip, savePath);
        return clip;
    }

    private static void CreateAnimations(out RuntimeAnimatorController playerController, out RuntimeAnimatorController enemyController)
    {
        // 1. Tạo Player Animations
        Sprite[] pIdleSprites = LoadSlicedSprites("Assets/Sprites/Player/player_idle.png");
        Sprite[] pRunSprites = LoadSlicedSprites("Assets/Sprites/Player/player_run.png");
        Sprite[] pJumpSprites = LoadSlicedSprites("Assets/Sprites/Player/player_jump.png");
        Sprite[] pAttackSprites = LoadSlicedSprites("Assets/Sprites/Player/player_attack.png");

        AnimationClip pIdleClip = CreateClip("Assets/Animations/Player_Idle.anim", pIdleSprites, 8f, true);
        AnimationClip pRunClip = CreateClip("Assets/Animations/Player_Run.anim", pRunSprites, 12f, true);
        AnimationClip pJumpClip = CreateClip("Assets/Animations/Player_Jump.anim", pJumpSprites, 8f, false);
        AnimationClip pAttackClip = CreateClip("Assets/Animations/Player_Attack.anim", pAttackSprites, 14f, false);

        // Player Animator Controller
        var pCtrl = AnimatorController.CreateAnimatorControllerAtPath("Assets/Animations/PlayerController.controller");
        pCtrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        pCtrl.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        pCtrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var pSm = pCtrl.layers[0].stateMachine;
        var pIdleState = pSm.AddState("Idle");
        pIdleState.motion = pIdleClip;
        pSm.defaultState = pIdleState;

        var pRunState = pSm.AddState("Run");
        pRunState.motion = pRunClip;

        var pJumpState = pSm.AddState("Jump");
        pJumpState.motion = pJumpClip;

        var pAttackState = pSm.AddState("Attack");
        pAttackState.motion = pAttackClip;

        // Transitions: Idle <-> Run
        var toRun = pIdleState.AddTransition(pRunState);
        toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        toRun.hasExitTime = false;
        toRun.duration = 0.05f;

        var toIdle = pRunState.AddTransition(pIdleState);
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        toIdle.hasExitTime = false;
        toIdle.duration = 0.05f;

        // Jump Transition
        var toJump = pSm.AddAnyStateTransition(pJumpState);
        toJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        toJump.hasExitTime = false;
        toJump.duration = 0.05f;

        var fromJumpToIdle = pJumpState.AddTransition(pIdleState);
        fromJumpToIdle.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        fromJumpToIdle.hasExitTime = false;
        fromJumpToIdle.duration = 0.05f;

        // Attack Transition
        var toAttack = pSm.AddAnyStateTransition(pAttackState);
        toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        toAttack.hasExitTime = false;
        toAttack.duration = 0.02f;

        var fromAttackToIdle = pAttackState.AddTransition(pIdleState);
        fromAttackToIdle.hasExitTime = true;
        fromAttackToIdle.exitTime = 0.85f;
        fromAttackToIdle.duration = 0.05f;

        playerController = pCtrl;

        // 2. Tạo Enemy Animations
        Sprite[] eIdleSprites = LoadSlicedSprites("Assets/Sprites/Enemies/enemy_idle.png");
        Sprite[] eWalkSprites = LoadSlicedSprites("Assets/Sprites/Enemies/enemy_walk.png");
        Sprite[] eAttackSprites = LoadSlicedSprites("Assets/Sprites/Enemies/enemy_attack.png");
        Sprite[] eDeathSprites = LoadSlicedSprites("Assets/Sprites/Enemies/enemy_death.png");

        AnimationClip eIdleClip = CreateClip("Assets/Animations/Enemy_Idle.anim", eIdleSprites, 6f, true);
        AnimationClip eWalkClip = CreateClip("Assets/Animations/Enemy_Walk.anim", eWalkSprites, 8f, true);
        AnimationClip eAttackClip = CreateClip("Assets/Animations/Enemy_Attack.anim", eAttackSprites, 10f, false);
        AnimationClip eDeathClip = CreateClip("Assets/Animations/Enemy_Death.anim", eDeathSprites, 7f, false);

        var eCtrl = AnimatorController.CreateAnimatorControllerAtPath("Assets/Animations/EnemyController.controller");
        eCtrl.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        eCtrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        eCtrl.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        var eSm = eCtrl.layers[0].stateMachine;
        var eIdleState = eSm.AddState("Idle");
        eIdleState.motion = eIdleClip;
        eSm.defaultState = eIdleState;

        var eWalkState = eSm.AddState("Walk");
        eWalkState.motion = eWalkClip;

        var eAttackState = eSm.AddState("Attack");
        eAttackState.motion = eAttackClip;

        var eDeathState = eSm.AddState("Death");
        eDeathState.motion = eDeathClip;

        // Idle <-> Walk
        var eToWalk = eIdleState.AddTransition(eWalkState);
        eToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        eToWalk.hasExitTime = false;
        eToWalk.duration = 0.05f;

        var eToIdle = eWalkState.AddTransition(eIdleState);
        eToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        eToIdle.hasExitTime = false;
        eToIdle.duration = 0.05f;

        // Attack Transition
        var eToAttack = eSm.AddAnyStateTransition(eAttackState);
        eToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        eToAttack.hasExitTime = false;
        eToAttack.duration = 0.02f;

        var eFromAttackToIdle = eAttackState.AddTransition(eIdleState);
        eFromAttackToIdle.hasExitTime = true;
        eFromAttackToIdle.exitTime = 0.85f;
        eFromAttackToIdle.duration = 0.05f;

        // Death Transition
        var eToDeath = eSm.AddAnyStateTransition(eDeathState);
        eToDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");
        eToDeath.hasExitTime = false;
        eToDeath.duration = 0.02f;

        enemyController = eCtrl;
        AssetDatabase.SaveAssets();
    }

    private static void CreatePrefabs(RuntimeAnimatorController playerController, RuntimeAnimatorController enemyController,
        out GameObject playerPrefab, out GameObject enemyPrefab, out GameObject groundPrefab, out GameObject platformPrefab, out GameObject spikePrefab)
    {
        Sprite[] pIdleFrames = LoadSlicedSprites("Assets/Sprites/Player/player_idle.png");
        Sprite knightSprite = (pIdleFrames != null && pIdleFrames.Length > 0) ? pIdleFrames[0] : LoadSprite("Assets/Sprites/Player/knight_placeholder.png");

        // Load vệt chém mới (slash.png)
        Sprite[] slashSprites = LoadSlicedSprites("Assets/Sprites/Player/slash.png");
        Sprite slashSprite = (slashSprites != null && slashSprites.Length > 2) ? slashSprites[2] : (slashSprites != null && slashSprites.Length > 0 ? slashSprites[0] : null);

        Sprite[] eIdleFrames = LoadSlicedSprites("Assets/Sprites/Enemies/enemy_idle.png");
        Sprite monsterSprite = (eIdleFrames != null && eIdleFrames.Length > 0) ? eIdleFrames[0] : LoadSprite("Assets/Sprites/Enemies/monster_placeholder.png");

        Sprite[] forestTiles = LoadSlicedSprites("Assets/Sprites/Environment/forest_tileset.png");
        Sprite groundSprite = (forestTiles != null && forestTiles.Length > 2) ? forestTiles[2] : (forestTiles != null && forestTiles.Length > 0 ? forestTiles[0] : LoadSprite("Assets/Sprites/Environment/ground_tile.png"));
        Sprite platformSprite = (forestTiles != null && forestTiles.Length > 8) ? forestTiles[8] : (forestTiles != null && forestTiles.Length > 1 ? forestTiles[1] : LoadSprite("Assets/Sprites/Environment/platform_tile.png"));

        // Load bẫy chông gai mới từ HP-enemy_count-spike_trap.png
        Sprite[] envItems = LoadSlicedSprites("Assets/Sprites/Environment/HP-enemy_count-spike_trap.png");
        Sprite spikeSprite = FindSpriteByName(envItems, "spike_trap") ?? (envItems != null && envItems.Length > 2 ? envItems[2] : LoadSprite("Assets/Sprites/Environment/spikes.png"));

        // --- 1. PLAYER PREFAB ---
        GameObject playerObj = new GameObject("Player");
        playerObj.tag = "Player";
        playerObj.layer = LayerMask.NameToLayer("Player") != -1 ? LayerMask.NameToLayer("Player") : 0;

        SpriteRenderer pRenderer = playerObj.AddComponent<SpriteRenderer>();
        pRenderer.sprite = knightSprite;
        pRenderer.sortingOrder = 10;

        if (playerController != null)
        {
            Animator pAnim = playerObj.AddComponent<Animator>();
            pAnim.runtimeAnimatorController = playerController;
        }

        Rigidbody2D pRb = playerObj.AddComponent<Rigidbody2D>();
        pRb.bodyType = RigidbodyType2D.Dynamic;
        pRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        pRb.freezeRotation = true;
        pRb.gravityScale = 3f;

        BoxCollider2D pCol = playerObj.AddComponent<BoxCollider2D>();
        pCol.size = new Vector2(0.85f, 1.45f);
        pCol.offset = new Vector2(0f, -0.2f);

        Health pHealth = playerObj.AddComponent<Health>();
        SerializedObject pHealthSo = new SerializedObject(pHealth);
        pHealthSo.FindProperty("maxHealth").intValue = 100;
        pHealthSo.FindProperty("isPlayer").boolValue = true;
        pHealthSo.FindProperty("invulnerabilityDuration").floatValue = 1.2f;
        pHealthSo.FindProperty("blinkInterval").floatValue = 0.1f;
        pHealthSo.ApplyModifiedProperties();

        // GroundCheck child
        GameObject groundCheckObj = new GameObject("GroundCheck");
        groundCheckObj.transform.SetParent(playerObj.transform);
        groundCheckObj.transform.localPosition = new Vector3(0, -0.92f, 0);

        PlayerMovement pMovement = playerObj.AddComponent<PlayerMovement>();
        SerializedObject pMoveSo = new SerializedObject(pMovement);
        pMoveSo.FindProperty("moveSpeed").floatValue = 7f;
        pMoveSo.FindProperty("jumpForce").floatValue = 13f;
        pMoveSo.FindProperty("groundCheckPoint").objectReferenceValue = groundCheckObj.transform;
        pMoveSo.FindProperty("groundCheckRadius").floatValue = 0.25f;
        pMoveSo.FindProperty("groundLayer").intValue = 1 << (LayerMask.NameToLayer("Ground") != -1 ? LayerMask.NameToLayer("Ground") : 0);
        pMoveSo.ApplyModifiedProperties();

        // AttackPoint child
        GameObject attackPointObj = new GameObject("AttackPoint");
        attackPointObj.transform.SetParent(playerObj.transform);
        attackPointObj.transform.localPosition = new Vector3(0.9f, -0.2f, 0);

        // Slash Visual Effect child (hiệu ứng chém mới từ slash.png)
        GameObject slashObj = new GameObject("SlashEffect");
        slashObj.transform.SetParent(playerObj.transform);
        slashObj.transform.localPosition = new Vector3(0.95f, 0f, 0);
        slashObj.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        SpriteRenderer slashRenderer = slashObj.AddComponent<SpriteRenderer>();
        slashRenderer.sprite = slashSprite;
        slashRenderer.sortingOrder = 11;
        slashObj.SetActive(false);

        PlayerCombat pCombat = playerObj.AddComponent<PlayerCombat>();
        SerializedObject pCombatSo = new SerializedObject(pCombat);
        pCombatSo.FindProperty("attackPoint").objectReferenceValue = attackPointObj.transform;
        pCombatSo.FindProperty("attackRange").floatValue = 1.1f;
        pCombatSo.FindProperty("attackDamage").intValue = 25;
        pCombatSo.FindProperty("attackCooldown").floatValue = 0.35f;
        pCombatSo.FindProperty("slashDuration").floatValue = 0.22f;
        pCombatSo.FindProperty("enemyLayers").intValue = 1 << (LayerMask.NameToLayer("Enemy") != -1 ? LayerMask.NameToLayer("Enemy") : 0);
        pCombatSo.FindProperty("slashVisualEffect").objectReferenceValue = slashObj;
        pCombatSo.FindProperty("knockbackForce").floatValue = 6.5f;

        // Gán mảng các frame vệt chém hoạt họa
        if (slashSprites != null && slashSprites.Length > 0)
        {
            SerializedProperty slashFramesProp = pCombatSo.FindProperty("slashFrames");
            if (slashFramesProp != null)
            {
                slashFramesProp.arraySize = slashSprites.Length;
                for (int i = 0; i < slashSprites.Length; i++)
                {
                    slashFramesProp.GetArrayElementAtIndex(i).objectReferenceValue = slashSprites[i];
                }
            }
        }
        pCombatSo.ApplyModifiedProperties();

        playerPrefab = PrefabUtility.SaveAsPrefabAsset(playerObj, "Assets/Prefabs/Player.prefab");
        DestroyImmediate(playerObj);

        // --- 2. ENEMY PREFAB ---
        GameObject enemyObj = new GameObject("Enemy");
        enemyObj.tag = "Enemy";
        enemyObj.layer = LayerMask.NameToLayer("Enemy") != -1 ? LayerMask.NameToLayer("Enemy") : 0;

        SpriteRenderer eRenderer = enemyObj.AddComponent<SpriteRenderer>();
        eRenderer.sprite = monsterSprite;
        eRenderer.sortingOrder = 5;

        if (enemyController != null)
        {
            Animator eAnim = enemyObj.AddComponent<Animator>();
            eAnim.runtimeAnimatorController = enemyController;
        }

        Rigidbody2D eRb = enemyObj.AddComponent<Rigidbody2D>();
        eRb.bodyType = RigidbodyType2D.Dynamic;
        eRb.freezeRotation = true;
        eRb.gravityScale = 3f;

        BoxCollider2D eCol = enemyObj.AddComponent<BoxCollider2D>();
        eCol.size = new Vector2(1.0f, 0.95f);
        eCol.offset = new Vector2(0f, -0.5f);

        Health eHealth = enemyObj.AddComponent<Health>();
        SerializedObject eHealthSo = new SerializedObject(eHealth);
        eHealthSo.FindProperty("maxHealth").intValue = 50;
        eHealthSo.FindProperty("isPlayer").boolValue = false;
        eHealthSo.FindProperty("invulnerabilityDuration").floatValue = 0f;
        eHealthSo.ApplyModifiedProperties();

        EnemyAI eAI = enemyObj.AddComponent<EnemyAI>();
        SerializedObject eAiSo = new SerializedObject(eAI);
        eAiSo.FindProperty("patrolSpeed").floatValue = 2.2f;
        eAiSo.FindProperty("chaseSpeed").floatValue = 3.6f;
        eAiSo.FindProperty("detectionRange").floatValue = 6f;
        eAiSo.FindProperty("attackRange").floatValue = 1.3f;
        eAiSo.FindProperty("attackDamage").intValue = 15;
        eAiSo.FindProperty("attackCooldown").floatValue = 1.0f;
        eAiSo.FindProperty("defaultPatrolDistance").floatValue = 3f;
        eAiSo.ApplyModifiedProperties();

        enemyPrefab = PrefabUtility.SaveAsPrefabAsset(enemyObj, "Assets/Prefabs/Enemy.prefab");
        DestroyImmediate(enemyObj);

        // --- 3. GROUND PREFAB ---
        GameObject groundObj = new GameObject("GroundBlock");
        groundObj.layer = LayerMask.NameToLayer("Ground") != -1 ? LayerMask.NameToLayer("Ground") : 0;
        SpriteRenderer gRenderer = groundObj.AddComponent<SpriteRenderer>();
        gRenderer.sprite = groundSprite;
        gRenderer.drawMode = SpriteDrawMode.Tiled;
        gRenderer.size = new Vector2(1f, 1f);
        BoxCollider2D gCol = groundObj.AddComponent<BoxCollider2D>();
        gCol.size = new Vector2(1f, 1f);
        groundPrefab = PrefabUtility.SaveAsPrefabAsset(groundObj, "Assets/Prefabs/GroundBlock.prefab");
        DestroyImmediate(groundObj);

        // --- 4. PLATFORM PREFAB ---
        GameObject platObj = new GameObject("Platform");
        platObj.layer = LayerMask.NameToLayer("Ground") != -1 ? LayerMask.NameToLayer("Ground") : 0;
        SpriteRenderer plRenderer = platObj.AddComponent<SpriteRenderer>();
        plRenderer.sprite = platformSprite;
        plRenderer.drawMode = SpriteDrawMode.Tiled;
        plRenderer.size = new Vector2(3f, 0.8f);
        BoxCollider2D plCol = platObj.AddComponent<BoxCollider2D>();
        plCol.size = new Vector2(3f, 0.8f);
        platformPrefab = PrefabUtility.SaveAsPrefabAsset(platObj, "Assets/Prefabs/Platform.prefab");
        DestroyImmediate(platObj);

        // --- 5. SPIKE HAZARD PREFAB ---
        GameObject spikeObj = new GameObject("SpikeHazard");
        spikeObj.layer = LayerMask.NameToLayer("Hazard") != -1 ? LayerMask.NameToLayer("Hazard") : 0;
        SpriteRenderer spRenderer = spikeObj.AddComponent<SpriteRenderer>();
        spRenderer.sprite = spikeSprite;
        spRenderer.sortingOrder = 4;
        BoxCollider2D spCol = spikeObj.AddComponent<BoxCollider2D>();
        spCol.isTrigger = true;
        spCol.size = new Vector2(1.5f, 0.7f);
        spCol.offset = new Vector2(0f, -0.2f);
        spikeObj.AddComponent<Hazard>();
        spikePrefab = PrefabUtility.SaveAsPrefabAsset(spikeObj, "Assets/Prefabs/SpikeHazard.prefab");
        DestroyImmediate(spikeObj);

        AssetDatabase.SaveAssets();
    }

    private static void BuildMainScene(GameObject playerPrefab, GameObject enemyPrefab, GameObject groundPrefab, GameObject platformPrefab, GameObject spikePrefab)
    {
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Camera & AudioListener
        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        camObj.transform.position = new Vector3(0, 2, -10);
        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.16f, 0.22f);
        camObj.AddComponent<AudioListener>();
        camObj.AddComponent<UniversalAdditionalCameraData>();

        CameraFollow2D camFollow = camObj.AddComponent<CameraFollow2D>();
        SerializedObject camFollowSo = new SerializedObject(camFollow);
        camFollowSo.FindProperty("smoothTime").floatValue = 0.2f;
        camFollowSo.FindProperty("offset").vector3Value = new Vector3(0f, 1.8f, -10f);
        camFollowSo.ApplyModifiedProperties();

        // 2. Global Light 2D
        GameObject lightObj = new GameObject("Global Light 2D");
        Light2D globalLight = lightObj.AddComponent<Light2D>();
        globalLight.lightType = Light2D.LightType.Global;
        globalLight.color = Color.white;
        globalLight.intensity = 1f;

        // 3. GameManager
        GameObject gmObj = new GameObject("GameManager");
        gmObj.AddComponent<GameManager>();

        // 4. Môi trường Background rừng cây (Parallax Forest Backdrop)
        GameObject envParent = new GameObject("--- ENVIRONMENT ---");
        Sprite bgSprite = LoadSprite("Assets/Sprites/Environment/forest_background.png");
        if (bgSprite != null)
        {
            // Trải dài 4 tấm background để phủ toàn bộ chiều dài màn chơi
            for (int i = 0; i < 4; i++)
            {
                GameObject bgObj = new GameObject($"ForestBackdrop_{i}");
                bgObj.transform.SetParent(envParent.transform);
                bgObj.transform.position = new Vector3(i * 16.6f - 5f, 3.5f, 5f);
                bgObj.transform.localScale = new Vector3(1.02f, 1.05f, 1f);
                SpriteRenderer bgRenderer = bgObj.AddComponent<SpriteRenderer>();
                bgRenderer.sprite = bgSprite;
                bgRenderer.sortingOrder = -50;
            }
        }

        // Tường vô hình chặn hai đầu biên giới màn chơi
        CreateInvisibleWall(envParent, -7.5f, 2.5f, 1f, 10f);
        CreateInvisibleWall(envParent, 41.5f, 2.5f, 1f, 10f);

        // Tạo chuỗi mặt đất bằng Tiled SpriteRenderer (từ x=-7 đến x=41, bề mặt y=-0.5f)
        CreateTiledGround(envParent, -7f, -2.5f, 48f, 2f, groundPrefab);

        // Các bục nhảy (Platforms) phân bố hợp lý với tầm nhảy của Hiệp sĩ
        CreatePlatform(envParent, 5.0f, 1.0f, 3.5f, platformPrefab);  // Đỉnh bục: y=1.4f
        CreatePlatform(envParent, 11.5f, 2.4f, 4.0f, platformPrefab); // Đỉnh bục: y=2.8f
        CreatePlatform(envParent, 18.0f, 1.2f, 4.0f, platformPrefab); // Đỉnh bục: y=1.6f
        CreatePlatform(envParent, 24.5f, 2.6f, 4.0f, platformPrefab); // Đỉnh bục: y=3.0f
        CreatePlatform(envParent, 32.0f, 1.2f, 4.0f, platformPrefab); // Đỉnh bục: y=1.6f

        // Bẫy chông gai (Spikes) đặt nổi trên mặt đất (y=0.10f) và bục nhảy (y=2.20f)
        CreateSpike(envParent, 8.5f, 0.10f, spikePrefab);
        CreateSpike(envParent, 9.8f, 0.10f, spikePrefab);
        CreateSpike(envParent, 21.0f, 0.10f, spikePrefab);
        CreateSpike(envParent, 22.3f, 0.10f, spikePrefab);
        CreateSpike(envParent, 18.5f, 2.20f, spikePrefab);

        // 5. Sinh Nhân vật (Player)
        GameObject playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        playerInstance.name = "Player";
        playerInstance.transform.position = new Vector3(-3.5f, 0.3f, 0);

        // Gán camera theo dõi player và giới hạn biên camera
        SerializedObject camSo = new SerializedObject(camFollow);
        camSo.FindProperty("target").objectReferenceValue = playerInstance.transform;
        camSo.FindProperty("useBounds").boolValue = true;
        camSo.FindProperty("minX").floatValue = -2.5f;
        camSo.FindProperty("maxX").floatValue = 36.5f;
        camSo.FindProperty("minY").floatValue = 1.0f;
        camSo.FindProperty("maxY").floatValue = 6.0f;
        camSo.ApplyModifiedProperties();

        // 6. Sinh 5 Quái vật (Enemies)
        GameObject enemiesParent = new GameObject("--- ENEMIES ---");
        CreateEnemy(enemiesParent, enemyPrefab, 4.0f, 0.0f, 1.5f, 6.0f);
        CreateEnemy(enemiesParent, enemyPrefab, 11.5f, 3.2f, 10.0f, 13.0f);
        CreateEnemy(enemiesParent, enemyPrefab, 15.5f, 0.0f, 13.5f, 17.5f);
        CreateEnemy(enemiesParent, enemyPrefab, 24.5f, 3.4f, 23.0f, 26.0f);
        CreateEnemy(enemiesParent, enemyPrefab, 33.0f, 0.0f, 29.5f, 36.0f);

        // 7. Tạo Canvas UI hoàn chỉnh
        BuildUI(playerInstance);

        // 8. Lưu Scene
        EditorSceneManager.SaveScene(newScene, "Assets/Scenes/MainScene.unity");
        AssetDatabase.Refresh();

        // Thêm scene vào Build Settings
        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[] {
            new EditorBuildSettingsScene("Assets/Scenes/MainScene.unity", true)
        };
        EditorBuildSettings.scenes = scenes;
    }

    private static void CreateTiledGround(GameObject parent, float startX, float startY, float width, float height, GameObject groundPrefab)
    {
        GameObject ground = (GameObject)PrefabUtility.InstantiatePrefab(groundPrefab);
        ground.name = "MainGround";
        ground.transform.SetParent(parent.transform);
        ground.transform.position = new Vector3(startX + width / 2f, startY + height / 2f, 0);

        SpriteRenderer sr = ground.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(width, height);
        }
        BoxCollider2D col = ground.GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.size = new Vector2(width, height);
        }
    }

    private static void CreatePlatform(GameObject parent, float x, float y, float width, GameObject platformPrefab)
    {
        GameObject plat = (GameObject)PrefabUtility.InstantiatePrefab(platformPrefab);
        plat.name = $"Platform_({x},{y})";
        plat.transform.SetParent(parent.transform);
        plat.transform.position = new Vector3(x, y, 0);

        SpriteRenderer sr = plat.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(width, 0.8f);
        }
        BoxCollider2D col = plat.GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.size = new Vector2(width, 0.8f);
        }
    }

    private static void CreateSpike(GameObject parent, float x, float y, GameObject spikePrefab)
    {
        GameObject spike = (GameObject)PrefabUtility.InstantiatePrefab(spikePrefab);
        spike.name = $"Spike_({x},{y})";
        spike.transform.SetParent(parent.transform);
        spike.transform.position = new Vector3(x, y, 0);
    }

    private static void CreateEnemy(GameObject parent, GameObject enemyPrefab, float x, float y, float minX, float maxX)
    {
        GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
        enemy.transform.SetParent(parent.transform);
        enemy.transform.position = new Vector3(x, y, 0);

        // Tạo 2 điểm tuần tra là con của parent (--- ENEMIES ---), không phải con của enemy!
        GameObject pA = new GameObject($"{enemy.name}_PatrolA");
        pA.transform.SetParent(parent.transform);
        pA.transform.position = new Vector3(minX, y, 0);

        GameObject pB = new GameObject($"{enemy.name}_PatrolB");
        pB.transform.SetParent(parent.transform);
        pB.transform.position = new Vector3(maxX, y, 0);

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai != null)
        {
            SerializedObject so = new SerializedObject(ai);
            so.FindProperty("patrolPointA").objectReferenceValue = pA.transform;
            so.FindProperty("patrolPointB").objectReferenceValue = pB.transform;
            so.ApplyModifiedProperties();
        }
    }

    private static void CreateInvisibleWall(GameObject parent, float x, float y, float width, float height)
    {
        GameObject wall = new GameObject($"BoundaryWall_({x})");
        wall.transform.SetParent(parent.transform);
        wall.transform.position = new Vector3(x, y, 0);
        wall.layer = LayerMask.NameToLayer("Ground") != -1 ? LayerMask.NameToLayer("Ground") : 0;
        BoxCollider2D col = wall.AddComponent<BoxCollider2D>();
        col.size = new Vector2(width, height);
    }

    private static void BuildUI(GameObject playerInstance)
    {
        // 1. Canvas
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // EventSystem
        GameObject esObj = new GameObject("EventSystem");
        esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // UIManager
        UIManager uiMgr = canvasObj.AddComponent<UIManager>();
        SerializedObject uiSo = new SerializedObject(uiMgr);

        // Font mặc định
        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // --- HUD PANEL ---
        GameObject hudPanel = new GameObject("HUD");
        hudPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform hudRt = hudPanel.AddComponent<RectTransform>();
        hudRt.anchorMin = new Vector2(0, 1);
        hudRt.anchorMax = new Vector2(1, 1);
        hudRt.pivot = new Vector2(0.5f, 1);
        hudRt.sizeDelta = new Vector2(0, 120);

        // Load HUD sprites từ HP-enemy_count-spike_trap.png
        Sprite[] hudSprites = LoadSlicedSprites("Assets/Sprites/Environment/HP-enemy_count-spike_trap.png");
        Sprite hpBarSprite = FindSpriteByName(hudSprites, "hp_bar");
        Sprite skullSprite = FindSpriteByName(hudSprites, "enemy_count_skull");

        // --- KHUNG THANH MÁU MỚI (HP BAR FRAME) ---
        GameObject hpFrameObj = new GameObject("HP_Bar_Frame");
        hpFrameObj.transform.SetParent(hudPanel.transform, false);
        RectTransform hpFrameRt = hpFrameObj.AddComponent<RectTransform>();
        hpFrameRt.anchorMin = new Vector2(0, 1);
        hpFrameRt.anchorMax = new Vector2(0, 1);
        hpFrameRt.pivot = new Vector2(0, 1);
        hpFrameRt.anchoredPosition = new Vector2(30, -25);
        hpFrameRt.sizeDelta = new Vector2(340, 78);

        Image hpFrameImg = hpFrameObj.AddComponent<Image>();
        if (hpBarSprite != null)
        {
            hpFrameImg.sprite = hpBarSprite;
        }
        else
        {
            hpFrameImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        }

        // Health Slider bên trong rãnh của khung ảnh hp_bar
        GameObject sliderObj = new GameObject("HealthSlider");
        sliderObj.transform.SetParent(hpFrameObj.transform, false);
        RectTransform sliderRt = sliderObj.AddComponent<RectTransform>();
        sliderRt.anchorMin = Vector2.zero;
        sliderRt.anchorMax = Vector2.one;
        sliderRt.offsetMin = new Vector2(46, 22);
        sliderRt.offsetMax = new Vector2(-40, -26);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0;
        slider.maxValue = 100;
        slider.value = 100;

        // Background slider (nền rãnh máu)
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.12f, 0.12f, 0.15f, 0.85f);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRt = fillArea.AddComponent<RectTransform>();
        fillAreaRt.anchorMin = Vector2.zero;
        fillAreaRt.anchorMax = Vector2.one;
        fillAreaRt.sizeDelta = Vector2.zero;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = new Color(0.9f, 0.15f, 0.2f); // Đỏ tươi
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta = Vector2.zero;

        slider.targetGraphic = fillImg;
        slider.fillRect = fillRt;
        uiSo.FindProperty("healthSlider").objectReferenceValue = slider;

        // Health Text
        GameObject hpTextObj = new GameObject("HealthText");
        hpTextObj.transform.SetParent(sliderObj.transform, false);
        Text hpText = hpTextObj.AddComponent<Text>();
        hpText.font = defaultFont;
        hpText.fontSize = 17;
        hpText.fontStyle = FontStyle.Bold;
        hpText.alignment = TextAnchor.MiddleCenter;
        hpText.color = Color.white;
        hpText.text = "100 / 100";
        var hpShadow = hpTextObj.AddComponent<Shadow>();
        hpShadow.effectColor = Color.black;
        hpShadow.effectDistance = new Vector2(1, -1);
        RectTransform hpTextRt = hpTextObj.GetComponent<RectTransform>();
        hpTextRt.anchorMin = Vector2.zero;
        hpTextRt.anchorMax = Vector2.one;
        hpTextRt.sizeDelta = Vector2.zero;
        uiSo.FindProperty("healthLegacyText").objectReferenceValue = hpText;

        // --- ENEMY COUNT GROUP (LOGO ĐẦU LÂU + SỐ LƯỢNG) ---
        GameObject enemyGroupObj = new GameObject("EnemyCountGroup");
        enemyGroupObj.transform.SetParent(hudPanel.transform, false);
        RectTransform enemyGroupRt = enemyGroupObj.AddComponent<RectTransform>();
        enemyGroupRt.anchorMin = new Vector2(1, 1);
        enemyGroupRt.anchorMax = new Vector2(1, 1);
        enemyGroupRt.pivot = new Vector2(1, 1);
        enemyGroupRt.anchoredPosition = new Vector2(-35, -25);
        enemyGroupRt.sizeDelta = new Vector2(210, 58);

        // Nền đen bán trong suốt cho nhóm đếm quái vật
        Image groupBg = enemyGroupObj.AddComponent<Image>();
        groupBg.color = new Color(0.08f, 0.08f, 0.12f, 0.75f);

        // Logo Đầu Lâu (enemy_count_skull)
        GameObject skullObj = new GameObject("SkullLogo");
        skullObj.transform.SetParent(enemyGroupObj.transform, false);
        RectTransform skullRt = skullObj.AddComponent<RectTransform>();
        skullRt.anchorMin = new Vector2(0, 0.5f);
        skullRt.anchorMax = new Vector2(0, 0.5f);
        skullRt.pivot = new Vector2(0, 0.5f);
        skullRt.anchoredPosition = new Vector2(10, 0);
        skullRt.sizeDelta = new Vector2(44, 44);

        Image skullImg = skullObj.AddComponent<Image>();
        if (skullSprite != null)
        {
            skullImg.sprite = skullSprite;
            skullImg.preserveAspect = true;
        }

        // Chữ số lượng quái vật (MonsterCounterText)
        GameObject monsterTextObj = new GameObject("MonsterCounterText");
        monsterTextObj.transform.SetParent(enemyGroupObj.transform, false);
        Text monsterText = monsterTextObj.AddComponent<Text>();
        monsterText.font = defaultFont;
        monsterText.fontSize = 24;
        monsterText.fontStyle = FontStyle.Bold;
        monsterText.alignment = TextAnchor.MiddleLeft;
        monsterText.color = new Color(1f, 0.88f, 0.25f); // Vàng kim sáng
        monsterText.text = "x 0 / 5";
        var mShadow = monsterTextObj.AddComponent<Shadow>();
        mShadow.effectColor = Color.black;
        mShadow.effectDistance = new Vector2(1.5f, -1.5f);

        RectTransform monsterTextRt = monsterTextObj.GetComponent<RectTransform>();
        monsterTextRt.anchorMin = Vector2.zero;
        monsterTextRt.anchorMax = Vector2.one;
        monsterTextRt.offsetMin = new Vector2(62, 0);
        monsterTextRt.offsetMax = new Vector2(-10, 0);
        uiSo.FindProperty("monsterCounterLegacyText").objectReferenceValue = monsterText;

        // --- GAME OVER PANEL ---
        GameObject goPanel = CreateModalPanel(canvasObj, "GameOverPanel", new Color(0.4f, 0.05f, 0.05f, 0.92f));
        CreateText(goPanel, "GAME OVER", 64, Color.red, defaultFont, new Vector2(0, 100));
        CreateText(goPanel, "Hiệp sĩ đã ngã xuống...", 24, Color.white, defaultFont, new Vector2(0, 30));
        Button goRestart = CreateButton(goPanel, "Chơi Lại", defaultFont, new Vector2(0, -50), new Color(0.2f, 0.6f, 0.2f));
        Button goQuit = CreateButton(goPanel, "Thoát Game", defaultFont, new Vector2(0, -130), new Color(0.5f, 0.15f, 0.15f));
        uiSo.FindProperty("gameOverPanel").objectReferenceValue = goPanel;
        uiSo.FindProperty("gameOverRestartButton").objectReferenceValue = goRestart;
        uiSo.FindProperty("gameOverQuitButton").objectReferenceValue = goQuit;

        // --- VICTORY PANEL ---
        GameObject vicPanel = CreateModalPanel(canvasObj, "VictoryPanel", new Color(0.05f, 0.25f, 0.1f, 0.94f));
        CreateText(vicPanel, "VICTORY!", 68, new Color(1f, 0.85f, 0.1f), defaultFont, new Vector2(0, 100));
        CreateText(vicPanel, "Bạn đã tiêu diệt toàn bộ quái vật bảo vệ vương quốc!", 24, Color.white, defaultFont, new Vector2(0, 30));
        Button vicRestart = CreateButton(vicPanel, "Chơi Lại", defaultFont, new Vector2(0, -50), new Color(0.2f, 0.6f, 0.2f));
        Button vicQuit = CreateButton(vicPanel, "Thoát Game", defaultFont, new Vector2(0, -130), new Color(0.5f, 0.15f, 0.15f));
        uiSo.FindProperty("victoryPanel").objectReferenceValue = vicPanel;
        uiSo.FindProperty("victoryRestartButton").objectReferenceValue = vicRestart;
        uiSo.FindProperty("victoryQuitButton").objectReferenceValue = vicQuit;

        // --- PAUSE PANEL ---
        GameObject pPanel = CreateModalPanel(canvasObj, "PausePanel", new Color(0.1f, 0.1f, 0.15f, 0.92f));
        CreateText(pPanel, "TẠM DỪNG (PAUSED)", 54, Color.white, defaultFont, new Vector2(0, 120));
        Button pResume = CreateButton(pPanel, "Tiếp Tục", defaultFont, new Vector2(0, 30), new Color(0.2f, 0.5f, 0.8f));
        Button pRestart = CreateButton(pPanel, "Chơi Lại", defaultFont, new Vector2(0, -45), new Color(0.2f, 0.6f, 0.2f));
        Button pQuit = CreateButton(pPanel, "Thoát Game", defaultFont, new Vector2(0, -120), new Color(0.5f, 0.15f, 0.15f));
        uiSo.FindProperty("pausePanel").objectReferenceValue = pPanel;
        uiSo.FindProperty("pauseResumeButton").objectReferenceValue = pResume;
        uiSo.FindProperty("pauseRestartButton").objectReferenceValue = pRestart;
        uiSo.FindProperty("pauseQuitButton").objectReferenceValue = pQuit;

        uiSo.ApplyModifiedProperties();
    }

    private static GameObject CreateModalPanel(GameObject canvas, string name, Color bgColor)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(canvas.transform, false);
        Image img = panel.AddComponent<Image>();
        img.color = bgColor;
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        panel.SetActive(false);
        return panel;
    }

    private static Text CreateText(GameObject parent, string content, int fontSize, Color color, Font font, Vector2 position)
    {
        GameObject tObj = new GameObject("Text");
        tObj.transform.SetParent(parent.transform, false);
        Text text = tObj.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = content;
        RectTransform rt = tObj.GetComponent<RectTransform>();
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(800, fontSize + 30);
        return text;
    }

    private static Button CreateButton(GameObject parent, string label, Font font, Vector2 position, Color normalColor)
    {
        GameObject btnObj = new GameObject($"Btn_{label}");
        btnObj.transform.SetParent(parent.transform, false);
        Image img = btnObj.AddComponent<Image>();
        img.color = normalColor;
        Button btn = btnObj.AddComponent<Button>();

        ColorBlock cb = btn.colors;
        cb.normalColor = normalColor;
        cb.highlightedColor = normalColor * 1.25f;
        cb.pressedColor = normalColor * 0.8f;
        btn.colors = cb;

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(260, 55);

        // Text child
        GameObject tObj = new GameObject("Text");
        tObj.transform.SetParent(btnObj.transform, false);
        Text text = tObj.AddComponent<Text>();
        text.font = font;
        text.fontSize = 24;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
        RectTransform tRt = tObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.sizeDelta = Vector2.zero;

        return btn;
    }
}
