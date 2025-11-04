using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// 布尔参数条件结构
[System.Serializable]
public struct BoolParameterCondition
{
    public string parameterName;    // 参数名称
    public bool requiredValue;      // 需要的参数值
}

// 状态转换配置组件
public class StateTransitionConfig : StateMachineBehaviour
{
    [Tooltip("下一个状态名称")]
    public string NextState = "";
    
    [Tooltip("触发转换所需的布尔参数条件列表")]
    public BoolParameterCondition[] BoolParameters;
    
    [Tooltip("是否在进入状态时立即检查转换条件")]
    public bool checkOnEnter = true;
    
    [Tooltip("是否在状态更新时持续检查转换条件")]
    public bool checkOnUpdate = true;
    
    // 状态进入时调用
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (checkOnEnter)
        {
            CheckAndTransition(animator);
        }
    }
    
    // 状态更新时调用
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (checkOnUpdate)
        {
            CheckAndTransition(animator);
        }
    }
    
    // 检查条件并执行状态转换
    private void CheckAndTransition(Animator animator)
    {
        if (string.IsNullOrEmpty(NextState) || BoolParameters == null)
            return;
        
        // 检查所有布尔参数条件是否满足
        bool allConditionsMet = true;
        foreach (var param in BoolParameters)
        {
            if (!string.IsNullOrEmpty(param.parameterName))
            {
                bool currentValue = animator.GetBool(param.parameterName);
                if (currentValue != param.requiredValue)
                {
                    allConditionsMet = false;
                    break;
                }
            }
        }
        
        // 如果所有条件都满足，则触发状态转换
        if (allConditionsMet && !string.IsNullOrEmpty(NextState))
        {
            animator.Play(NextState);
        }
    }
    
    // 在Inspector中显示调试信息
    public override string ToString()
    {
        string debugInfo = $"Next State: {NextState}\nConditions: ";
        
        if (BoolParameters != null && BoolParameters.Length > 0)
        {
            foreach (var param in BoolParameters)
            {
                debugInfo += $"\n- {param.parameterName} == {param.requiredValue}";
            }
        }
        else
        {
            debugInfo += "None";
        }
        
        return debugInfo;
    }
}