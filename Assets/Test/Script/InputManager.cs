using UnityEngine;
using System;



public class InputEventManager : MonoBehaviour
{
    // 定义事件类型（示例：移动、跳跃、攻击）
    public static event Action<Vector3> OnMoveInput;        // 移动事件（携带方向参数）
    public static event Action OnJumpPressed;               // 跳跃事件（无参数）
    public static event Action<int> OnAttackTriggered;      // 攻击事件（携带攻击类型参数）
    public static event Action<float> OnMouse_X;            // 跳跃事件（无参数）
    public static event Action<float> OnMouse_Y;            // 跳跃事件（无参数）
    public static event Action<bool> OnSprint;                    // 跳跃事件（无参数）


    private static bool CurrentSprintState = false;

    [Header("鼠标控制设置")]
    [Tooltip("是否在游戏开始时隐藏鼠标")]
    public bool hideOnStart = true;
    [Tooltip("是否在游戏开始时锁定鼠标")]
    public bool lockOnStart = true;

    void Start()
    {
        // 初始化鼠标状态
        SetMouseState(hideOnStart, lockOnStart);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        // 当游戏窗口重新获得焦点时恢复鼠标状态
        if (hasFocus)
        {
            SetMouseState(hideOnStart, lockOnStart);
        }
    }

    private void SetMouseState(bool visible, bool locked)
    {
        Cursor.visible = !visible;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
    }

    void Update()
    {
        // 统一检测输入并触发事件
        HandleMovementInput();
        HandleJumpInput();
        HandleAttackInput();
        HandleOnMouseMoveX();
        HandleOnMouseMoveY();
        HandleOnSprint();
    }

    private void HandleMovementInput()
    {
        //传递输入值 X,Y,Z
        Vector3 input = new Vector3(Input.GetAxis("MoveRight"), 0, Input.GetAxis("MoveForward"));
        OnMoveInput?.Invoke(input); // 触发移动输入事件
    }

    private void HandleJumpInput()
    {
        if (Input.GetButtonDown("Jump"))
            OnJumpPressed?.Invoke();// 触发跳跃事件
    }

    private void HandleAttackInput()
    {
        if (Input.GetButtonDown("Attack"))
            OnAttackTriggered?.Invoke(1); // 触发攻击事件
    }

    private void HandleOnMouseMoveX()
    {
        OnMouse_X?.Invoke((float)Input.GetAxis("Mouse_X")); // 触发鼠标输入事件X
    }
    private void HandleOnMouseMoveY()
    {
        OnMouse_Y?.Invoke((float)Input.GetAxis("Mouse_Y")); // 触发鼠标输入事件Y
    }
    
    private void HandleOnSprint()
    {
        if (Input.GetButtonDown("Sprint") != CurrentSprintState)
        {
            CurrentSprintState = Input.GetButtonDown("Sprint");
            if (CurrentSprintState)
            {
                OnSprint?.Invoke(true); // 触发攻击事件
            }
            else
            {
                OnSprint?.Invoke(false); // 撤销攻击事件
            }
        }


            
    }

}
