using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class SpringArmComponent : MonoBehaviour
{
    private Quaternion _rotation = Quaternion.Euler(0, 0, 0);

    [Header("Spring Arm Settings")]
    [Tooltip("弹簧臂目标长度")]
    public float targetArmLength = 3.0f;
    [Tooltip("启用碰撞检测")]
    public bool enableCollision = true;
    [Tooltip("碰撞检测层掩码")]
    public LayerMask collisionLayers;
    [Tooltip("碰撞缓冲距离")]
    public float collisionPadding = 0.2f;
    [Tooltip("摄像机延迟跟随的平滑时间")]
    public float cameraLagSpeed = 0.2f;
    // 私有变量
    private Camera UserCamera;
    private Vector3 _cameraVelocity;
    private float _currentArmLength;
    private RaycastHit _hitInfo;
    private Vector3 _rotationAngles;
    private Vector3 _fixedPivotPosition; // 新增：存储固定起点位置

    [Header("Rotation Settings")]
    [SerializeField] private Vector2 _pitchLimit = new Vector2(-80f, 80f); // Pitch旋转范围限制
    [SerializeField] private float _rotationSpeed = 180f; // 旋转速度
    [SerializeField] private float _rotationSmoothness = 10f; // 旋转平滑度
    private float _currentPitch = 0f; // 当前Pitch角度
    private Quaternion _targetRotation; // 目标旋转

    private Quaternion OriginalRotation;//初始世界旋转
    private Quaternion TargetRotation;//初始世界旋转

    private void Awake()
    {
        UserCamera = GetComponentInChildren<Camera>();
        if (UserCamera != null)
        {
            // 初始化固定起点位置为当前挂载点的世界位置
            _fixedPivotPosition = transform.position;

            // 强制初始化摄像机位置
            UserCamera.transform.position = _fixedPivotPosition - transform.forward * targetArmLength;
            _cameraVelocity = Vector3.zero;
        }
        _currentArmLength = targetArmLength;
        _targetRotation = transform.rotation;
    }

    private void Start(){
        //OriginalRotation = transform.rotation; // 保存初始世界旋转
        OriginalRotation = Quaternion.identity;
    }

    private void Update()
    {
        // 平滑旋转
        //transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, _rotationSmoothness * Time.deltaTime);

        transform.rotation = OriginalRotation;

    }

    private void LateUpdate()
    {

        // 更新固定起点位置（如果需要跟随移动的物体）
        _fixedPivotPosition = transform.position;


        // 计算目标位置（基于固定起点和当前旋转）
        Vector3 desiredCameraPos = _fixedPivotPosition - transform.forward * _currentArmLength;

        

        // 碰撞检测
        if (enableCollision)
        {
            if (Physics.Raycast(
                _fixedPivotPosition, // 使用固定起点
                -transform.forward,
                out _hitInfo,
                targetArmLength,
                collisionLayers))
            {
                _currentArmLength = Mathf.Max(0, _hitInfo.distance - collisionPadding);
            }
            else
            {
                _currentArmLength = targetArmLength;
            }
        }

        // 平滑移动摄像机
        if (UserCamera != null)
        {
            UserCamera.transform.position = Vector3.SmoothDamp(
                UserCamera.transform.position,
                desiredCameraPos,
                ref _cameraVelocity,
                Mathf.Max(0.01f, cameraLagSpeed));

            // 摄像机始终看向固定起点
            //UserCamera.transform.LookAt(_fixedPivotPosition);
        }

    }


    // 规范化角度到[-180,180]范围
    private float NormalizeAngle(float angle)
    {
        while(angle > 180) angle -= 360;
        while(angle < -180) angle += 360;
        return angle;
    }


    public void AddRotation(float delta_Yaw, float delta_Pitch)
    {
        // 处理Yaw旋转（绕世界Y轴）
        if (delta_Yaw != 0)
        {
            Quaternion yawRotation = Quaternion.AngleAxis(delta_Yaw * _rotationSpeed * Time.deltaTime, Vector3.up);
            OriginalRotation = yawRotation * OriginalRotation;
        }

        // 处理Pitch旋转（绕相机右轴）
        if (delta_Pitch != 0)
        {
            // 计算新的Pitch角度并限制范围
            float newPitch = _currentPitch + delta_Pitch * _rotationSpeed * Time.deltaTime;
            newPitch = Mathf.Clamp(newPitch, _pitchLimit.x, _pitchLimit.y);

            // 计算实际需要旋转的角度
            float actualDeltaPitch = newPitch - _currentPitch;
            _currentPitch = newPitch;

            if (Mathf.Abs(actualDeltaPitch) > Mathf.Epsilon)
            {
                Quaternion pitchRotation = Quaternion.AngleAxis(actualDeltaPitch, UserCamera.transform.right);
                OriginalRotation = pitchRotation * OriginalRotation;
            }
        }
    }



    // 新增函数：直接设置旋转角度
    public void SetRotation(Vector3 angle)
    {
        _rotationAngles = angle;
        _rotationAngles.x = Mathf.Clamp(_rotationAngles.x, -80f, 80f);
        transform.localEulerAngles = _rotationAngles;
    }

    // 编辑器可视化
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position - transform.forward * targetArmLength);
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawLine(_fixedPivotPosition, _fixedPivotPosition - transform.forward * _currentArmLength);

        if (enableCollision)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_fixedPivotPosition - transform.forward * _currentArmLength, 0.1f);
        }
    }
}