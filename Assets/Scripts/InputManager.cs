using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 방향키(Arrow Keys) 이동 및 왼손 Q, W, E, R 전투 액션 키 입력 감지 매니저
/// W키 충돌을 방지하기 위해 이동은 방향키(↑↓←→)로 전담
/// </summary>
[DefaultExecutionOrder(-100)]
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    /// <summary>
    /// Q, W, E, R 액션 키 입력 이벤트 (KeyCode 전달)
    /// </summary>
    public static event Action<KeyCode> OnCombatKeyPressed;

    /// <summary>
    /// 현재 방향키 이동 입력 벡터 (정규화)
    /// </summary>
    public static Vector2 MoveInput { get; private set; }

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLog = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        HandleMovementInput();
        HandleCombatInput();
    }

    private void HandleMovementInput()
    {
        Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            // 방향키(Arrow Keys)로 회피 및 이동
            if (Keyboard.current.upArrowKey.isPressed) input.y += 1f;
            if (Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
            if (Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
            if (Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
        }
#else
        if (Input.GetKey(KeyCode.UpArrow)) input.y += 1f;
        if (Input.GetKey(KeyCode.DownArrow)) input.y -= 1f;
        if (Input.GetKey(KeyCode.LeftArrow)) input.x -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) input.x += 1f;
#endif

        MoveInput = input.sqrMagnitude > 1f ? input.normalized : input;
    }

    private void HandleCombatInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return;

        if (Keyboard.current.qKey.wasPressedThisFrame) BroadcastCombatKey(KeyCode.Q);
        if (Keyboard.current.wKey.wasPressedThisFrame) BroadcastCombatKey(KeyCode.W);
        if (Keyboard.current.eKey.wasPressedThisFrame) BroadcastCombatKey(KeyCode.E);
        if (Keyboard.current.rKey.wasPressedThisFrame) BroadcastCombatKey(KeyCode.R);
#else
        if (Input.GetKeyDown(KeyCode.Q)) BroadcastCombatKey(KeyCode.Q);
        if (Input.GetKeyDown(KeyCode.W)) BroadcastCombatKey(KeyCode.W);
        if (Input.GetKeyDown(KeyCode.E)) BroadcastCombatKey(KeyCode.E);
        if (Input.GetKeyDown(KeyCode.R)) BroadcastCombatKey(KeyCode.R);
#endif
    }

    private void BroadcastCombatKey(KeyCode key)
    {
        if (showDebugLog)
        {
            Debug.Log($"[InputManager] Combat Key Pressed: <color=#00FFAA>{key}</color>");
        }

        OnCombatKeyPressed?.Invoke(key);
    }
}
