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
    public static event Action<bool> OnSprint;              // 跳跃事件（无参数）
    private SpringArmComponent SpringArm;
    GameObject character;

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

        character = GameObject.Find("PF_Player");
        character = character ? character : GameObject.Find("PF_Player(Clone)");

        SpringArm = character.transform.GetChild(0)?.GetComponentInChildren<SpringArmComponent>(true);

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

        if (character == null)
            return;


        Vector3 cp = character.transform.position;
        Vector3 forward = character.transform.forward;
        // 1. 绘制线段（红色）
        //DrawDebug.DrawLine(cp, cp + forward * 2, Color.black, Time.deltaTime);

        // // 2. 绘制球体（蓝色，半径1，16边，永久显示）
        //DrawDebug.DrawSphere(character.transform.position, 0.5f, Color.blue, 4, Time.deltaTime);
        //DrawDebug.DrawSphere(character.transform.position,0.1f, Color.red, 4,Time.deltaTime);

        // // 3. 绘制正方体（绿色，尺寸1x1x1，持续3秒）
        //DrawDebug.DrawCube(character.transform.position, new Vector3(1,1,1), Color.green, Time.deltaTime,0.01f);

        // // 4. 绘制圆锥体（黄色，底面半径0.5，12边，持续4秒）
        //DrawDebug.DrawConeWire(cp, cp + forward * 2, 0.5f, Color.green, 12, 0.01f, Time.deltaTime);

        // DrawDebug.DrawArrow(cp, cp + forward * 2, 0.15f, 0.5f, 12, Color.red, Time.deltaTime);

        //DrawDebug.DrawAxis(SpringArm.transform.position, SpringArm.transform, 2, 0.1f, Time.deltaTime, 0.05f);
        // // 5. 绘制文字（白色，永久显示）
        DrawDebug.DrawTextCoroutine(cp, forward,10,"Hello World!", Color.blue, 0.1f);
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
