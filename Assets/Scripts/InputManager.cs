using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Q, W, E, R 키 입력 및 방향키 이동 입력 감지 매니저
/// New Input System과 Legacy Input 지원
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
    /// 현재 이동 입력 벡터 (WASD / 방향키 벡터)
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
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
        }
#else
        input.x = Input.GetAxisRaw("Horizontal");
        input.y = Input.GetAxisRaw("Vertical");
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
