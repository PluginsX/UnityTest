using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 比较方式枚举（与Unity原生动画条件保持一致）
public enum AnimatorConditionMode
{
    If = 0,               // Bool为true
    IfNot = 1,            // Bool为false
    Greater = 2,          // 数值>阈值
    Less = 3,             // 数值<阈值
    Equals = 4,           // 数值==阈值
    NotEqual = 5,         // 数值!=阈值
    Trigger = 6           // Trigger被激活
}

// 条件类定义
[Serializable]
public class Condition
{
    public string parameterName;          // 参数名称
    public AnimatorConditionMode mode;    // 比较方式
    public float threshold;               // 阈值（仅数值类型有效）
}

// Condition类的自定义属性绘制器（实现横向布局）
[CustomPropertyDrawer(typeof(Condition))]
public class ConditionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // 横向布局区域划分
        float totalWidth = position.width;
        float paramWidth = totalWidth * 0.35f;       // 参数选择框宽度
        float modeWidth = totalWidth * 0.3f;         // 比较方式宽度
        float thresholdWidth = totalWidth * 0.3f;    // 阈值输入框宽度
        float spacing = 5f;                          // 间距

        // 1. 绘制参数选择框
        SerializedProperty paramProp = property.FindPropertyRelative("parameterName");
        Rect paramRect = new Rect(position.x, position.y, paramWidth, position.height);
        EditorGUI.PropertyField(paramRect, paramProp, GUIContent.none);

        // 2. 绘制比较方式下拉框
        SerializedProperty modeProp = property.FindPropertyRelative("mode");
        Rect modeRect = new Rect(paramRect.xMax + spacing, position.y, modeWidth, position.height);
        EditorGUI.PropertyField(modeRect, modeProp, GUIContent.none);

        // 3. 绘制阈值输入框（根据参数类型动态显示）
        SerializedProperty thresholdProp = property.FindPropertyRelative("threshold");
        Rect thresholdRect = new Rect(modeRect.xMax + spacing, position.y, thresholdWidth, position.height);

        // 获取当前参数类型，决定是否显示阈值框
        AnimatorControllerParameterType paramType = GetParameterType(property);
        if (paramType == AnimatorControllerParameterType.Float ||
            paramType == AnimatorControllerParameterType.Int)
        {
            EditorGUI.PropertyField(thresholdRect, thresholdProp, GUIContent.none);
        }
        else
        {
            // 非数值类型显示空白
            EditorGUI.LabelField(thresholdRect, "");
        }

        EditorGUI.EndProperty();
    }

    // 辅助方法：获取当前参数的类型
    private AnimatorControllerParameterType GetParameterType(SerializedProperty property)
    {
        // 从组件中获取可用参数列表和类型列表
        SerializedObject parentObj = property.serializedObject;
        SerializedProperty paramNamesProp = parentObj.FindProperty("availableParameters");
        SerializedProperty paramTypesProp = parentObj.FindProperty("parameterTypes");
        string currentParamName = property.FindPropertyRelative("parameterName").stringValue;

        // 查找参数对应的类型
        for (int i = 0; i < paramNamesProp.arraySize; i++)
        {
            if (paramNamesProp.GetArrayElementAtIndex(i).stringValue == currentParamName)
            {
                return (AnimatorControllerParameterType)paramTypesProp.GetArrayElementAtIndex(i).enumValueIndex;
            }
        }
        return AnimatorControllerParameterType.Float;
    }
}

// 核心组件类
[RequireComponent(typeof(Animator))]
public class CrossFadeByParameter : StateMachineBehaviour
{
    [Header("过渡目标设置")]
    public string nextStateName = "";                  // 目标状态名称

    [Header("过渡参数")]
    public bool hasExitTime = false;                   // 是否使用退出时间
    [Range(0f, 1f)] public float exitTime = 0.5f;      // 退出时间（0-1表示归一化时间）
    public bool useFixedDuration = false;              // 是否使用固定持续时间
    public float transitionOffset = 0f;                // 过渡偏移
    [Min(0f)] public float transitionDuration = 0.25f; // 过渡持续时间
    public AnimationCurve blendCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f); // 混合曲线
    public bool canBeInterrupted = true;               // 是否可被中断

    [Header("过渡条件")]
    public Condition[] conditions;                     // 条件列表

    // 编辑器用：缓存可用参数和类型
    [HideInInspector] public string[] availableParameters;
    [HideInInspector] public AnimatorControllerParameterType[] parameterTypes;

    // 内部状态变量
    private bool isTransitioning = false;
    private float transitionStartTime = 0f;
    private int currentTransitionLayer = -1;

    // 编辑器模式下更新参数列表
    private void OnValidate()
    {
        AnimatorController controller = GetCurrentAnimatorController();
        if (controller != null)
        {
            UpdateParameterList(controller);
        }
    }

    // 获取当前状态所属的Animator控制器
    private AnimatorController GetCurrentAnimatorController()
    {
        if (this == null) return null;

        try
        {
            // 通过序列化获取StateMachineBehaviour关联的控制器
            SerializedObject serializedBehaviour = new SerializedObject(this);
            SerializedProperty controllerProp = serializedBehaviour.FindProperty("m_Controller");
            if (controllerProp != null && controllerProp.objectReferenceValue is AnimatorController controller)
            {
                return controller;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"获取Animator控制器失败: {e.Message}");
        }
        return null;
    }

    // 更新可用参数列表
    private void UpdateParameterList(AnimatorController controller)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;

        // 初始化参数数组（+1留空选项）
        availableParameters = new string[parameters.Length + 1];
        parameterTypes = new AnimatorControllerParameterType[parameters.Length + 1];

        availableParameters[0] = "";
        parameterTypes[0] = AnimatorControllerParameterType.Float;

        // 填充参数数据
        for (int i = 0; i < parameters.Length; i++)
        {
            availableParameters[i + 1] = parameters[i].name;
            parameterTypes[i + 1] = parameters[i].type;
        }
    }

    // 右键菜单：添加条件
    [ContextMenu("添加条件")]
    public void AddCondition()
    {
        int newLength = (conditions != null) ? conditions.Length + 1 : 1;
        Array.Resize(ref conditions, newLength);
        conditions[newLength - 1] = new Condition();
    }

    // 右键菜单：清除所有条件
    [ContextMenu("清除所有条件")]
    public void ClearConditions()
    {
        conditions = null;
    }

    // 状态进入时调用
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ResetTransitionState();
        if (!hasExitTime)
        {
            CheckAndTransition(animator, stateInfo, layerIndex);
        }
    }

    // 状态更新时调用
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

        // 处理退出时间逻辑
        if (hasExitTime)
        {
            float normalizedTime = stateInfo.normalizedTime % 1f; // 取模处理循环动画
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

    // 状态退出时调用
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ResetTransitionState();
    }

    // 检查条件并触发过渡
    private void CheckAndTransition(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 验证目标状态和条件
        if (string.IsNullOrEmpty(nextStateName))
        {
            Debug.LogWarning("未设置目标状态名称，无法过渡");
            return;
        }

        if (conditions == null || conditions.Length == 0)
        {
            Debug.LogWarning("未添加过渡条件，无法过渡");
            return;
        }

        // 检查所有条件是否满足
        if (CheckAllConditions(animator))
        {
            StartTransition(animator, stateInfo, layerIndex);
        }
    }

    // 检查所有条件是否满足
    private bool CheckAllConditions(Animator animator)
    {
        foreach (Condition condition in conditions)
        {
            if (!CheckSingleCondition(animator, condition))
            {
                return false;
            }
        }
        return true;
    }

    // 检查单个条件是否满足
    private bool CheckSingleCondition(Animator animator, Condition condition)
    {
        // 空参数直接返回false
        if (string.IsNullOrEmpty(condition.parameterName))
        {
            return false;
        }

        // 获取参数信息
        AnimatorControllerParameter param = Array.Find(animator.parameters, p => p.name == condition.parameterName);
        if (param == null)
        {
            Debug.LogWarning($"参数 {condition.parameterName} 不存在于Animator控制器中");
            return false;
        }

        // 根据参数类型和比较方式判断
        switch (param.type)
        {
            case AnimatorControllerParameterType.Bool:
                return CheckBoolCondition(animator.GetBool(condition.parameterName), condition.mode);

            case AnimatorControllerParameterType.Int:
                return CheckNumericCondition(animator.GetInteger(condition.parameterName), condition);

            case AnimatorControllerParameterType.Float:
                return CheckNumericCondition(animator.GetFloat(condition.parameterName), condition);

            case AnimatorControllerParameterType.Trigger:
                return CheckTriggerCondition(animator, condition.parameterName, condition.mode);

            default:
                return false;
        }
    }

    // 检查布尔类型条件
    private bool CheckBoolCondition(bool currentValue, AnimatorConditionMode mode)
    {
        switch (mode)
        {
            case AnimatorConditionMode.If:
                return currentValue;
            case AnimatorConditionMode.IfNot:
                return !currentValue;
            default:
                // 布尔类型不支持其他比较方式
                return false;
        }
    }

    // 检查数值类型条件
    private bool CheckNumericCondition(float currentValue, Condition condition)
    {
        switch (condition.mode)
        {
            case AnimatorConditionMode.Greater:
                return currentValue > condition.threshold;
            case AnimatorConditionMode.Less:
                return currentValue < condition.threshold;
            case AnimatorConditionMode.Equals:
                return Mathf.Approximately(currentValue, condition.threshold);
            case AnimatorConditionMode.NotEqual:
                return !Mathf.Approximately(currentValue, condition.threshold);
            default:
                // 数值类型不支持If/IfNot/Trigger
                return false;
        }
    }

    // 检查触发器类型条件
    private bool CheckTriggerCondition(Animator animator, string parameterName, AnimatorConditionMode mode)
    {
        if (mode != AnimatorConditionMode.Trigger)
        {
            return false;
        }

        // 检测Trigger是否被激活（旧版本兼容方式）
        bool isTriggered = animator.GetBool(parameterName);
        if (isTriggered)
        {
            // 触发后立即重置，避免重复触发
            animator.ResetTrigger(parameterName);
            return true;
        }
        return false;
    }

    // 开始过渡
    private void StartTransition(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        transitionStartTime = Time.time;
        currentTransitionLayer = layerIndex;

        // 执行交叉淡入过渡（按位置传递参数，兼容所有版本） 
        animator.CrossFade(
            nextStateName,          // 第一个参数：目标状态名称
            transitionDuration,     // 第二个参数：过渡持续时间（fadeLength）
            layerIndex,             // 第三个参数：层索引
            transitionOffset        // 第四个参数：归一化时间偏移
        );
    }

    // 更新过渡进度
    private void UpdateTransitionProgress()
    {
        if (!isTransitioning)
        {
            return;
        }

        float progress = GetTransitionProgress();
        if (progress >= 1f)
        {
            ResetTransitionState();
        }
    }

    // 重置过渡状态
    private void ResetTransitionState()
    {
        isTransitioning = false;
        transitionStartTime = 0f;
        currentTransitionLayer = -1;
    }

    // 获取过渡进度（0-1）
    private float GetTransitionProgress()
    {
        if (!isTransitioning || transitionDuration <= 0)
        {
            return 0f;
        }

        float elapsedTime = Time.time - transitionStartTime;
        return Mathf.Clamp01(elapsedTime / transitionDuration);
    }
}