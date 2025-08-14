using UnityEngine;

public class RootMotionTransfer : MonoBehaviour
{
    public Transform skeleton;
    private Transform rootBone; // 骨架的根骨骼
    private Vector3 lastRootPosition; // 上一帧的根骨骼位置
    private Vector3 initialLocalPosition; // 初始局部位置

    [Header("Debug Settings")]
    public float axisLength = 100;//Debug轴向绘制

    private void Start()
    {
        // 查找子对象中的骨架网格体
        
        if (skeleton == null)
        {
            Debug.LogError("未指定骨架网格体子对象！");
            return;
        }

        // 获取Animator组件并确保启用Root Motion
        Animator animator = skeleton.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("SKM_Charactor_Standerd上未找到Animator组件！");
            return;
        }
        animator.applyRootMotion = true;

        // 获取根骨骼（Humanoid动画使用hips，Generic动画使用指定的根骨骼）
        rootBone = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : skeleton;

        // 记录初始位置
        initialLocalPosition = skeleton.localPosition;
        lastRootPosition = rootBone.position;
    }

    private void LateUpdate()
    {
        if (rootBone == null) return;

        // 计算根骨骼的位移差
        Vector3 deltaPosition = rootBone.position - lastRootPosition;

        // 将位移应用到父级Prefab
        transform.position += deltaPosition;

        // 重置骨架网格体的局部位置
        Transform skeleton = rootBone;
        while (skeleton.parent != transform && skeleton.parent != null)
        {
            skeleton = skeleton.parent;
        }
        skeleton.localPosition = initialLocalPosition;

        // 更新记录的位置
        lastRootPosition = rootBone.position;
    }

    private void Update()
    {
        Debug.Log("DrawRay");
        DrawDebug.DrawAxis(GameObject.Find("PF_Player(Clone)").transform.position, GameObject.Find("PF_Player(Clone)").transform, axisLength, 0.1f, Time.deltaTime, 0.02f);
    }

}