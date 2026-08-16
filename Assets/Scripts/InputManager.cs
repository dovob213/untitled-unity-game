using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 방향키(Arrow Keys) 이동, 타겟팅 키(Q,W,E,R), 처형 공격 키(A,S,D,F) 입력 감지 매니저
/// </summary>
[DefaultExecutionOrder(-100)]
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    /// <summary>
    /// Q, W, E, R 타겟 선택 키 입력 이벤트
    /// </summary>
    public static event Action<KeyCode> OnTargetKeyPressed;

    /// <summary>
    /// A, S, D, F 처형/공격 키 입력 이벤트 (입력 버퍼링용)
    /// </summary>
    public static event Action<KeyCode> OnAttackKeyPressed;

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
        HandleTargetInput();
        HandleAttackInput();
    }

    private void HandleMovementInput()
    {
        Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
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

    private void HandleTargetInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return;

        if (Keyboard.current.qKey.wasPressedThisFrame) BroadcastTargetKey(KeyCode.Q);
        if (Keyboard.current.wKey.wasPressedThisFrame) BroadcastTargetKey(KeyCode.W);
        if (Keyboard.current.eKey.wasPressedThisFrame) BroadcastTargetKey(KeyCode.E);
        if (Keyboard.current.rKey.wasPressedThisFrame) BroadcastTargetKey(KeyCode.R);
#else
        if (Input.GetKeyDown(KeyCode.Q)) BroadcastTargetKey(KeyCode.Q);
        if (Input.GetKeyDown(KeyCode.W)) BroadcastTargetKey(KeyCode.W);
        if (Input.GetKeyDown(KeyCode.E)) BroadcastTargetKey(KeyCode.E);
        if (Input.GetKeyDown(KeyCode.R)) BroadcastTargetKey(KeyCode.R);
#endif
    }

    private void HandleAttackInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return;

        if (Keyboard.current.aKey.wasPressedThisFrame) BroadcastAttackKey(KeyCode.A);
        if (Keyboard.current.sKey.wasPressedThisFrame) BroadcastAttackKey(KeyCode.S);
        if (Keyboard.current.dKey.wasPressedThisFrame) BroadcastAttackKey(KeyCode.D);
        if (Keyboard.current.fKey.wasPressedThisFrame) BroadcastAttackKey(KeyCode.F);
#else
        if (Input.GetKeyDown(KeyCode.A)) BroadcastAttackKey(KeyCode.A);
        if (Input.GetKeyDown(KeyCode.S)) BroadcastAttackKey(KeyCode.S);
        if (Input.GetKeyDown(KeyCode.D)) BroadcastAttackKey(KeyCode.D);
        if (Input.GetKeyDown(KeyCode.F)) BroadcastAttackKey(KeyCode.F);
#endif
    }

    private void BroadcastTargetKey(KeyCode key)
    {
        if (showDebugLog)
        {
            Debug.Log($"[InputManager] 🎯 Target Selected (QWER): <color=#00FFAA>{key}</color>");
        }

        OnTargetKeyPressed?.Invoke(key);
    }

    private void BroadcastAttackKey(KeyCode key)
    {
        if (showDebugLog)
        {
            Debug.Log($"[InputManager] ⚔️ Attack Key Pressed (ASDF): <color=#FFCC00>{key}</color>");
        }

        OnAttackKeyPressed?.Invoke(key);
    }
}
