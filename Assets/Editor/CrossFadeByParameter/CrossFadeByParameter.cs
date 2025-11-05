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

[System.Serializable]
public class CrossFadeByParameter : StateMachineBehaviour
{
    // [Header("Transition Settings")]
    public string nextStateName = "";
    public bool hasExitTime = false;
    [Range(0f, 1f)] public float exitTime = 0f;
    public bool useFixedDuration = false;
    
    // [Header("Mix Settings")]
    public float transitionOffset = 0f;
    [Min(0f)] public float transitionDuration = 0.25f;
    public AnimationCurve blendCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    
    // [Header("Transition Control")]
    public bool canBeInterrupted = true;
    
    // [Header("Conditions")]
    public Condition[] conditions;
    
    [System.Serializable]
    public class Condition
    {
        public string parameter;
        public CompareOperator mode;
        public float threshold;
    }
    
    // 内部状态变量
    private bool isTransitioning = false;
    private float transitionStartTime = 0f;
    private int currentTransitionLayer = -1;
    
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ResetTransitionState();
        
        if (!hasExitTime)
        {
            CheckAndTransition(animator, stateInfo, layerIndex);
        }
    }
    
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (isTransitioning)
        {
            if (canBeInterrupted)
            {
                CheckAndTransition(animator, stateInfo, layerIndex);
            }
            else
            {
                UpdateTransitionProgress();
            }
            return;
        }
        
        if (hasExitTime)
        {
            float normalizedTime = stateInfo.normalizedTime;
            float exitThreshold = useFixedDuration ? exitTime : exitTime * stateInfo.length;
            
            if (normalizedTime >= exitThreshold)
            {
                CheckAndTransition(animator, stateInfo, layerIndex);
            }
        }
        else
        {
            CheckAndTransition(animator, stateInfo, layerIndex);
        }
    }
    
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ResetTransitionState();
    }
    
    private void CheckAndTransition(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (string.IsNullOrEmpty(nextStateName) || conditions == null || conditions.Length == 0)
            return;
        
        if (CheckAllConditions(animator))
        {
            StartTransition(animator, stateInfo, layerIndex);
        }
    }
    
    private bool CheckAllConditions(Animator animator)
    {
        foreach (var condition in conditions)
        {
            if (!CheckSingleCondition(animator, condition))
                return false;
        }
        return true;
    }
    
    private bool CheckSingleCondition(Animator animator, Condition condition)
    {
        if (string.IsNullOrEmpty(condition.parameter))
            return false;
            
        // 获取参数当前值
        float currentValue = 0f;
        if (animator.IsParameterControlledByCurve(condition.parameter))
        {
            currentValue = animator.GetFloat(condition.parameter);
        }
        else
        {
            // 尝试获取不同类型的参数值
            var param = GetAnimatorParameter(animator, condition.parameter);
            if (param != null)
            {
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
                    case AnimatorControllerParameterType.Trigger:
                        currentValue = animator.GetBool(condition.parameter) ? 1f : 0f;
                        break;
                }
            }
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
    
    private AnimatorControllerParameter GetAnimatorParameter(Animator animator, string paramName)
    {
        foreach (var param in animator.parameters)
        {
            if (param.name == paramName)
                return param;
        }
        return null;
    }
    
    private void StartTransition(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (isTransitioning) return;
        
        isTransitioning = true;
        transitionStartTime = Time.time;
        currentTransitionLayer = layerIndex;
        
        animator.CrossFade(nextStateName, transitionDuration, layerIndex, transitionOffset);
    }
    
    private void UpdateTransitionProgress()
    {
        if (!isTransitioning) return;
        
        float progress = GetTransitionProgress();
        if (progress >= 1f)
        {
            ResetTransitionState();
        }
    }
    
    private void ResetTransitionState()
    {
        isTransitioning = false;
        transitionStartTime = 0f;
        currentTransitionLayer = -1;
    }
    
    private float GetTransitionProgress()
    {
        if (!isTransitioning) return 0f;
        float elapsed = Time.time - transitionStartTime;
        return Mathf.Clamp01(elapsed / transitionDuration);
    }
}