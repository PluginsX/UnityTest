using System;
using System.Text;
using UnityEngine;

public enum CompareOperator 
{ 
    Greater,    // >
    Less,       // <
    Equals,     // ==
    NotEqual    // !=
}
/// <summary>
/// 转换条件类
/// </summary>
[System.Serializable]
public class Condition
{
    /// <summary>
    /// String 条件变量名
    /// </summary>
    public string parameter;
    /// <summary>
    /// CompareOperator 比较运算符
    /// </summary>
    public CompareOperator mode;
    /// <summary>
    /// 比较值
    /// </summary>
    public float threshold;
}

[System.Serializable]
public class CrossFadeByParameter : StateMachineBehaviour
{
    public string nextStateName = "";
    public bool hasExitTime = false;
    [Range(0f, 1f)] public float exitTime = 0f;
    public bool useFixedDuration = false;
    public float transitionOffset = 0f;
    [Min(0f)] public float transitionDuration = 0.25f;
    public AnimationCurve blendCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public bool canBeInterrupted = true;


    // 转换条件列表
    public Condition[] conditions;
    
    // 内部状态变量
    // 正在过渡中
    private bool isTransitioning = false;
    // 过渡开始时间
    private float transitionStartTime = 0f;
    // 当前过渡的层
    private int currentTransitionLayer = -1;
    
    // 当进入该状态时
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        UnityEngine.Debug.Log("OnStateEnter()");
        // 重置过渡状态
        ResetTransitionState();
        
        // 如果没有强制混出时间
        if (!hasExitTime)
        {
            // 检查条件和过渡
            CheckAndTransition(animator, stateInfo, layerIndex);
        }
    }
    
    // 进入该状态后每一帧执行
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 正在转换
        if (isTransitioning)
        {
            // 可以被打断
            if (canBeInterrupted)
            {
                // 检查条件和过渡
                CheckAndTransition(animator, stateInfo, layerIndex);
            }
            else
            {
                // 更新过渡进程
                UpdateTransitionProgress();
            }
            return;
        }
        
        // 如果有强制混出位置
        if (hasExitTime)
        {
            // 标准化时间，当前状态动画播放的时间
            float normalizedTime = stateInfo.normalizedTime;
            // 退出时间，如果启用的固定持续时间则直接使用ExitTime的值，否则就用比例X片段长度
            float exitThreshold = useFixedDuration ? exitTime : exitTime * stateInfo.length;
            // 如果标准化时间>=混出时间
            if (normalizedTime >= exitThreshold)
            {
                // 检查混出条件并混出
                CheckAndTransition(animator, stateInfo, layerIndex);
            }
        }
        else
        {
            // 检查混出条件并混出
            CheckAndTransition(animator, stateInfo, layerIndex);
        }
    }
    
    // 混出该状态时
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 重置过渡状态
        ResetTransitionState();
    }

    // 检查条件和混出
    private void CheckAndTransition(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (string.IsNullOrEmpty(nextStateName) || conditions == null || conditions.Length == 0)
        {
            Debug.Log("无法过渡：缺少目标状态或条件");
            return;
        }

        bool conditionsMet = CheckAllConditions(animator);
        Debug.Log($"条件检查结果: {conditionsMet}");

        if (conditionsMet)
        {
            Debug.Log($"开始过渡到: {nextStateName}");
            StartTransition(animator, stateInfo, layerIndex);
        }
    }


    // 检查所有条件
    private bool CheckAllConditions(Animator animator)
    {
        // 遍历条件列表
        foreach (var condition in conditions)
        {
            // 检查单一条件
            if (!CheckSingleCondition(animator, condition))
                return false;
        }
        return true;
    }

    // 修复CheckSingleCondition方法中的Trigger处理：
    private bool CheckSingleCondition(Animator animator, Condition condition)
    {
        if (string.IsNullOrEmpty(condition.parameter))
            return false;

        // 获取参数类型
        AnimatorControllerParameter param = GetAnimatorParameter(animator, condition.parameter);
        if (param == null) return false;

        float currentValue = 0f;

        switch (param.type)
        {
            case AnimatorControllerParameterType.Float:
                currentValue = animator.GetFloat(condition.parameter);
                break;
            case AnimatorControllerParameterType.Int:
                currentValue = animator.GetInteger(condition.parameter);
                break;
            case AnimatorControllerParameterType.Bool:
                currentValue = animator.GetBool(condition.parameter) ? 1f : 0f;
                break;
            // 替换原来的 GetTrigger 用法
            case AnimatorControllerParameterType.Trigger:
                // 旧版本用 GetBool 检测 Trigger 状态
                bool isTriggerActive = animator.GetBool(condition.parameter);
                if (isTriggerActive)
                {
                    // 手动重置重置 Trigger（关键：避免重复触发）
                    animator.ResetTrigger(condition.parameter);
                    return true;
                }
                break;

            default:
                return false;
        }

        // 根据比较操作符检查条件
        switch (condition.mode)
        {
            case CompareOperator.Greater:
                return currentValue > condition.threshold;
            case CompareOperator.Less:
                return currentValue < condition.threshold;
            case CompareOperator.Equals:
                return Mathf.Approximately(currentValue, condition.threshold);
            case CompareOperator.NotEqual:
                return !Mathf.Approximately(currentValue, condition.threshold);
            default:
                return false;
        }
    }

    // 根据参数名从Animator获取参数
    private AnimatorControllerParameter GetAnimatorParameter(Animator animator, string paramName)
    {
        // 遍历所有参数
        foreach (var param in animator.parameters)
        {
            // 对比变量名
            if (param.name == paramName)
                return param;
        }
        // 否则为空
        return null;
    }
    
    // 开始过渡
    private void StartTransition(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (isTransitioning) return;
        
        isTransitioning = true;
        transitionStartTime = Time.time;
        currentTransitionLayer = layerIndex;
        
        // 调用CrossFade(目标状态名，过渡时间，动画层序号，目标动画起始偏移)
        animator.CrossFade(nextStateName, transitionDuration, layerIndex, transitionOffset);
    }
    

    // 更新过渡进度
    private void UpdateTransitionProgress()
    {
        // 如果有正在进行的过渡则不执行
        if (!isTransitioning) return;
        // 获取过渡进度
        float progress = GetTransitionProgress();
        // 过渡进度>=1 表示完成进度
        if (progress >= 1f)
        {
            // 重置转换状态
            ResetTransitionState();
        }
    }
    
    // 重置转换状态
    private void ResetTransitionState()
    {
        // 是否正在过渡中
        isTransitioning = false;
        // 过渡开始时间
        transitionStartTime = 0f;
        // 当前过渡所处的动画层
        currentTransitionLayer = -1;
    }
    
    // 获取CrossFade过渡进度
    private float GetTransitionProgress()
    {
        if (!isTransitioning) return 0f;
        // 混出持续的时间 = 当前状态播放总时长 - 混出开始时间
        float elapsed = Time.time - transitionStartTime;
        // Mathf.Clamp01(vf)作用是将一个浮点数限制在0到1的范围内
        return Mathf.Clamp01(elapsed / transitionDuration);
    }
}