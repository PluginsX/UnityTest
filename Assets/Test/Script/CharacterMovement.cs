// 示例：角色移动控制脚本
using UnityEngine;


public class CharacterMovement : MonoBehaviour
{

    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 5f; // 移动速度
    [SerializeField] private bool autoFaceMovement = true; // 是否自动面向移动方向
    [SerializeField] private float rotationSpeed = 10f; // 旋转平滑速度
    [SerializeField] private float acceleration = 15f; // 新增加速度参数
    [SerializeField] private Transform meshRoot;
    
    private Rigidbody PlayerCollinder; // 假设这是角色的Rigidbody组件
    private Vector3 lastMovementDirection; // 记录最后移动方向
    private Vector3 targetVelocity; // 存储目标速度而非直接修改物理速度
    private SpringArmComponent SpringArm;
    private Animator AnimatorControler;
    private Camera MainCamera;

    private void Awake()
    {
        PlayerCollinder = GetComponent<Rigidbody>();
        MainCamera = Camera.main;
    }

    private void OnEnable()
    {
        // 订阅事件
        InputEventManager.OnMoveInput += HandleMovement;
        InputEventManager.OnJumpPressed += Jump;
        InputEventManager.OnMouse_X += HandleMouseMoveX;
        InputEventManager.OnMouse_Y += HandleMouseMoveY;
        InputEventManager.OnAttackTriggered += HandleAttack;
        InputEventManager.OnSprint += HandleSprint;

    }
    private void Start()
    {
        AnimatorControler = transform.GetChild(0)?.GetComponentInChildren<Animator>(true);
        PlayerCollinder = GetComponent<Rigidbody>();
        SpringArm = transform.GetChild(0)?.GetComponentInChildren<SpringArmComponent>(true);
        MainCamera = SpringArm.GetComponentInChildren<Camera>(true);
    }

    private void OnDisable()
    {
        // 必须取消订阅！避免内存泄漏[3,6](@ref)
        InputEventManager.OnMoveInput -= HandleMovement;
        InputEventManager.OnJumpPressed -= Jump;
        InputEventManager.OnMouse_X -= HandleMouseMoveX;
        InputEventManager.OnMouse_Y -= HandleMouseMoveY;
        InputEventManager.OnAttackTriggered -= HandleAttack;
        InputEventManager.OnSprint -= HandleSprint;
    }


    private void HandleAttack(int SkillNumber)
    {
        //Debug.Log("HandleAttack");
        AnimatorControler.SetTrigger("Attack");
    }

    // 事件处理方法
    private void HandleMovement(Vector3 inputDirection)
    {
        if (MainCamera == null) return;
        
        Vector3 cameraForward = Vector3.Cross(MainCamera.transform.right, Vector3.up).normalized;// 前方向：摄像机右方向与世界Y轴的叉乘（即摄像机正前方投影到水平面）
        Vector3 cameraRight = MainCamera.transform.right;// 右方向：直接使用摄像机右方向（已水平化）
        cameraRight.y = 0;//压平向上轴，仅保留水平方向
        cameraRight.Normalize();//仅保留方向信息，与大小无关
        Vector3 moveDirection = (cameraForward * inputDirection.z) + (cameraRight * inputDirection.x);// 组合移动方向
        moveDirection.Normalize();// 确保方向归一化（防止斜向移动速度更快）

        // 组合输入方向（物理系统独立处理Y轴）
        targetVelocity = (cameraForward * inputDirection.z + cameraRight * inputDirection.x) * moveSpeed;
        targetVelocity.y = PlayerCollinder.velocity.y; // 保持物理系统计算的垂直速度（重力/跳跃等）

        
        // 旋转控制（与物理解耦）
        if (inputDirection != Vector3.zero && autoFaceMovement)
        {
            //this.transform.LookAt(transform.position + moveDirection);

            lastMovementDirection = moveDirection;
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }
    private void FixedUpdate()
    {
        // 在FixedUpdate中平滑应用速度（不影响物理模拟）
        PlayerCollinder.velocity = Vector3.Lerp(
            PlayerCollinder.velocity,//当前的速度
            targetVelocity,//目标速度
            acceleration * Time.fixedDeltaTime
        );
    }


    private void Jump()
    {
        //Debug.Log("PerformJump");
        PlayerCollinder.AddForce(Vector3.up * 50000f * Time.deltaTime, ForceMode.Impulse);
    }

    private void HandleMouseMoveX(float x)
    {
        //Debug.Log("HandleMouseMove:["+x+"|"+y+"]");
        SpringArm.AddRotation(x, 0);
    }

    private void HandleMouseMoveY(float y)
    {
        //Debug.Log("HandleMouseMove:["+x+"|"+y+"]");
        SpringArm.AddRotation(0, y);

    }

    private void HandleSprint(bool press)
    {
        //Debug.Log("HandleMouseMove:["+x+"|"+y+"]");
        AnimatorControler.SetBool("Sprint", press);

    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + lastMovementDirection * 2);
    }

}