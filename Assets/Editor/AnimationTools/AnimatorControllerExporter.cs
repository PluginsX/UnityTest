using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;

public class AnimatorControllerExporter : EditorWindow
{
    private AnimatorController selectedController;
    private string fileName = "AnimatorStateConfigReport";
    private Vector2 scrollPosition;
    
    // 组件类型选择相关
    private Type selectedBehaviourType;
    private string[] allBehaviourTypeNames;
    private Type[] allBehaviourTypes;
    private int selectedTypeIndex = 0;
    
    // 参数筛选相关
    private Dictionary<string, bool> parameterSelection = new Dictionary<string, bool>();
    private Vector2 parameterScrollPosition;
    private bool selectAllParameters = true;

    // 新增：是否导出Transition条件
    private bool exportTransitionConditions = true;
    private bool exportStateBehaviours = true;

    [MenuItem("Assets/导出状态机配置报告", false, 30)]
    public static void ExportAnimatorConfig()
    {
        var selectedObject = Selection.activeObject;
        
        if (selectedObject == null || !(selectedObject is AnimatorController))
        {
            EditorUtility.DisplayDialog("错误", "请选择一个Animator Controller资产！", "确定");
            return;
        }

        var window = CreateInstance<AnimatorControllerExporter>();
        window.selectedController = selectedObject as AnimatorController;
        window.InitializeBehaviourTypes();
        window.ShowUtility();
    }

    private void InitializeBehaviourTypes()
    {
        List<Type> behaviourTypes = new List<Type>();
        List<string> typeNames = new List<string>();
        
        typeNames.Add("无 (显示所有公共字段)");
        behaviourTypes.Add(null);
        
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsSubclassOf(typeof(StateMachineBehaviour)) && !type.IsAbstract)
                    {
                        behaviourTypes.Add(type);
                        typeNames.Add(type.Name);
                    }
                }
            }
            catch (System.Reflection.ReflectionTypeLoadException)
            {
                continue;
            }
        }
        
        allBehaviourTypes = behaviourTypes.ToArray();
        allBehaviourTypeNames = typeNames.ToArray();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        GUILayout.Label("Animator Controller状态配置导出工具", EditorStyles.boldLabel);
        GUILayout.Space(10);

        selectedController = EditorGUILayout.ObjectField("目标控制器", selectedController, typeof(AnimatorController), false) as AnimatorController;
        fileName = EditorGUILayout.TextField("文件名", fileName);
        
        GUILayout.Space(10);
        
        // 新增：导出选项
        GUILayout.Label("导出选项:", EditorStyles.boldLabel);
        exportTransitionConditions = EditorGUILayout.Toggle("导出Transition条件", exportTransitionConditions);
        exportStateBehaviours = EditorGUILayout.Toggle("导出State Behaviours", exportStateBehaviours);
        
        GUILayout.Space(10);
        
        // 组件类型选择
        GUILayout.Label("选择要分析的组件类型:", EditorStyles.boldLabel);
        if (allBehaviourTypeNames != null && allBehaviourTypeNames.Length > 0)
        {
            int newIndex = EditorGUILayout.Popup("组件类型", selectedTypeIndex, allBehaviourTypeNames);
            if (newIndex != selectedTypeIndex)
            {
                selectedTypeIndex = newIndex;
                selectedBehaviourType = allBehaviourTypes[newIndex];
                UpdateParameterList();
            }
        }
        
        GUILayout.Space(10);
        
        // 参数筛选界面
        if (selectedBehaviourType != null && parameterSelection.Count > 0 && exportStateBehaviours)
        {
            DrawParameterSelectionPanel();
        }
        
        GUILayout.Space(20);

        EditorGUILayout.EndScrollView();
        
        if (GUILayout.Button("导出Markdown报告", GUILayout.Height(30)))
        {
            if (selectedController != null)
            {
                ExportToMarkdown();
            }
            else
            {
                EditorUtility.DisplayDialog("错误", "请选择有效的Animator Controller！", "确定");
            }
        }

        if (GUILayout.Button("关闭", GUILayout.Height(25)))
        {
            Close();
        }
    }

    private void DrawParameterSelectionPanel()
    {
        GUILayout.Label("选择要导出的参数:", EditorStyles.boldLabel);
        
        // 全选/取消全选按钮
        EditorGUILayout.BeginHorizontal();
        bool newSelectAll = EditorGUILayout.Toggle("全选", selectAllParameters);
        if (newSelectAll != selectAllParameters)
        {
            selectAllParameters = newSelectAll;
            var keys = new List<string>(parameterSelection.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                parameterSelection[keys[i]] = selectAllParameters;
            }
        }
        
        if (GUILayout.Button("反选", GUILayout.Width(60)))
        {
            var keys = new List<string>(parameterSelection.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                parameterSelection[keys[i]] = !parameterSelection[keys[i]];
            }
            UpdateSelectAllState();
        }
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(5);
        
        // 参数列表
        GUILayout.Label($"参数列表 ({GetSelectedParameterCount()}/{parameterSelection.Count} 已选择):", EditorStyles.miniLabel);
        
        parameterScrollPosition = EditorGUILayout.BeginScrollView(parameterScrollPosition, GUILayout.Height(150));
        
        var paramKeys = new List<string>(parameterSelection.Keys);
        for (int i = 0; i < paramKeys.Count; i++)
        {
            string paramKey = paramKeys[i];
            bool isSelected = parameterSelection[paramKey];
            
            EditorGUILayout.BeginHorizontal();
            bool newValue = EditorGUILayout.Toggle(paramKey, isSelected, GUILayout.Width(250));
            
            if (newValue != isSelected)
            {
                parameterSelection[paramKey] = newValue;
                UpdateSelectAllState();
            }
            
            GUILayout.Label(GetParameterTypeDisplay(paramKey), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();
    }

    private void UpdateParameterList()
    {
        parameterSelection.Clear();
        
        if (selectedBehaviourType != null)
        {
            var fields = selectedBehaviourType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                if (!field.IsSpecialName)
                {
                    string key = $"字段: {field.Name}";
                    parameterSelection[key] = true;
                }
            }
            
            var properties = selectedBehaviourType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                if (property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    string key = $"属性: {property.Name}";
                    parameterSelection[key] = true;
                }
            }
        }
        
        selectAllParameters = true;
    }

    private string GetParameterTypeDisplay(string parameterKey)
    {
        if (selectedBehaviourType == null) return "";
        
        string paramName = parameterKey.Substring(parameterKey.IndexOf(":") + 2);
        
        if (parameterKey.StartsWith("字段:"))
        {
            var field = selectedBehaviourType.GetField(paramName);
            return field != null ? $"({field.FieldType.Name})" : "";
        }
        else if (parameterKey.StartsWith("属性:"))
        {
            var property = selectedBehaviourType.GetProperty(paramName);
            return property != null ? $"({property.PropertyType.Name})" : "";
        }
        
        return "";
    }

    private void UpdateSelectAllState()
    {
        selectAllParameters = true;
        var keys = new List<string>(parameterSelection.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            if (!parameterSelection[keys[i]])
            {
                selectAllParameters = false;
                break;
            }
        }
    }

    private int GetSelectedParameterCount()
    {
        int count = 0;
        var keys = new List<string>(parameterSelection.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            if (parameterSelection[keys[i]]) count++;
        }
        return count;
    }

    private void ExportToMarkdown()
    {
        string path = EditorUtility.SaveFilePanel("导出为Markdown", "", fileName, "md");
        
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            StringBuilder mdContent = new StringBuilder();
            mdContent.AppendLine($"# Animator Controller状态配置报告");
            mdContent.AppendLine($"**控制器名称**: {selectedController.name}");
            mdContent.AppendLine($"**分析组件**: {(selectedBehaviourType != null ? selectedBehaviourType.Name : "所有公共字段")}");
            mdContent.AppendLine($"**导出参数**: {GetSelectedParameterCount()}/{parameterSelection.Count}");
            mdContent.AppendLine($"**导出时间**: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            mdContent.AppendLine("---");

            // 导出参数列表
            ExportParameters(mdContent);

            // 遍历所有Layer
            for (int layerIndex = 0; layerIndex < selectedController.layers.Length; layerIndex++)
            {
                var layer = selectedController.layers[layerIndex];
                mdContent.AppendLine($"\n## Layer {layerIndex}: {layer.name}");
                mdContent.AppendLine($"- **权重**: {layer.defaultWeight}");
                mdContent.AppendLine($"- **遮罩**: {(layer.avatarMask != null ? layer.avatarMask.name : "None")}");

                ProcessStateMachine(layer.stateMachine, mdContent, "", layerIndex);
            }

            File.WriteAllText(path, mdContent.ToString());
            EditorUtility.DisplayDialog("导出成功", $"Markdown报告已保存至: {path}", "确定");
            Debug.Log($"状态机配置报告已导出: {path}");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("导出错误", $"导出过程中发生错误: {e.Message}", "确定");
            Debug.LogError($"导出错误: {e}");
        }
    }

    // 新增：导出参数列表
    private void ExportParameters(StringBuilder mdContent)
    {
        mdContent.AppendLine("\n## 控制器参数列表");
        
        if (selectedController.parameters.Length == 0)
        {
            mdContent.AppendLine("无参数");
            return;
        }

        mdContent.AppendLine("| 参数名 | 类型 | 默认值 | 说明 |");
        mdContent.AppendLine("|-------|------|--------|------|");

        for (int i = 0; i < selectedController.parameters.Length; i++)
        {
            var param = selectedController.parameters[i];
            string defaultValue = GetParameterDefaultValue(param);
            mdContent.AppendLine($"| {param.name} | {param.type} | {defaultValue} | - |");
        }
    }

    private string GetParameterDefaultValue(AnimatorControllerParameter param)
    {
        switch (param.type)
        {
            case AnimatorControllerParameterType.Float:
                return param.defaultFloat.ToString("F2");
            case AnimatorControllerParameterType.Int:
                return param.defaultInt.ToString();
            case AnimatorControllerParameterType.Bool:
                return param.defaultBool.ToString();
            case AnimatorControllerParameterType.Trigger:
                return "Trigger";
            default:
                return "Unknown";
        }
    }

    private void ProcessStateMachine(AnimatorStateMachine stateMachine, StringBuilder mdContent, string indent, int layerIndex)
    {
        if (stateMachine == null) return;

        mdContent.AppendLine($"\n{indent}### 状态机: {stateMachine.name}");

        // 处理当前状态机中的所有状态
        for (int i = 0; i < stateMachine.states.Length; i++)
        {
            var childState = stateMachine.states[i];
            AnimatorState state = childState.state;
            
            mdContent.AppendLine($"\n{indent}#### 动画状态: {state.name}");
            mdContent.AppendLine($"{indent}- **位置**: ({childState.position.x}, {childState.position.y})");
            mdContent.AppendLine($"{indent}- **运动**: {(state.motion != null ? state.motion.name : "None")}");
            mdContent.AppendLine($"{indent}- **速度**: {state.speed}");
            mdContent.AppendLine($"{indent}- **循环**: {state.cycleOffset}");

            // 导出State Behaviours
            if (exportStateBehaviours)
            {
                ExportStateBehaviours(state, mdContent, indent);
            }

            // 导出Transitions和Conditions
            if (exportTransitionConditions)
            {
                ExportStateTransitions(state, mdContent, indent);
            }
        }

        // 导出Any State的Transitions
        if (exportTransitionConditions && stateMachine.anyStateTransitions.Length > 0)
        {
            mdContent.AppendLine($"\n{indent}#### Any State 过渡");
            for (int i = 0; i < stateMachine.anyStateTransitions.Length; i++)
            {
                ExportTransition(stateMachine.anyStateTransitions[i], mdContent, indent, "Any State");
            }
        }

        // 导出Entry状态的Transitions
        if (exportTransitionConditions && stateMachine.entryTransitions.Length > 0)
        {
            mdContent.AppendLine($"\n{indent}#### Entry 过渡");
            for (int i = 0; i < stateMachine.entryTransitions.Length; i++)
            {
                ExportTransition(stateMachine.entryTransitions[i], mdContent, indent, "Entry");
            }
        }

        // 递归处理子状态机
        for (int i = 0; i < stateMachine.stateMachines.Length; i++)
        {
            var childStateMachine = stateMachine.stateMachines[i];
            ProcessStateMachine(childStateMachine.stateMachine, mdContent, indent + "  ", layerIndex);
        }
    }

    // 新增：导出State Behaviours
    private void ExportStateBehaviours(AnimatorState state, StringBuilder mdContent, string indent)
    {
        StateMachineBehaviour[] behaviours = state.behaviours;
        if (behaviours.Length == 0) return;

        mdContent.AppendLine($"\n{indent}##### State Behaviours");

        List<StateMachineBehaviour> targetBehaviours = new List<StateMachineBehaviour>();

        if (selectedBehaviourType != null)
        {
            for (int j = 0; j < behaviours.Length; j++)
            {
                var behaviour = behaviours[j];
                if (behaviour.GetType() == selectedBehaviourType)
                {
                    targetBehaviours.Add(behaviour);
                }
            }
        }
        else
        {
            targetBehaviours.AddRange(behaviours);
        }

        for (int k = 0; k < targetBehaviours.Count; k++)
        {
            var behaviour = targetBehaviours[k];
            string behaviourName = behaviour.GetType().Name;
            mdContent.AppendLine($"{indent}- **Component**: {behaviourName}");
            
            var selectedParams = GetSelectedParameters(behaviour);
            
            var paramKeys = new List<string>(selectedParams.Keys);
            for (int m = 0; m < paramKeys.Count; m++)
            {
                string key = paramKeys[m];
                mdContent.AppendLine($"{indent}  - {key}: {selectedParams[key]}");
            }
        }
    }

    // 新增：导出State的Transitions
    private void ExportStateTransitions(AnimatorState state, StringBuilder mdContent, string indent)
    {
        if (state.transitions.Length == 0) return;

        mdContent.AppendLine($"\n{indent}##### 过渡条件");
        
        for (int i = 0; i < state.transitions.Length; i++)
        {
            ExportTransition(state.transitions[i], mdContent, indent, state.name);
        }
    }

    // 修复：正确导出Transition信息
    private void ExportTransition(AnimatorTransitionBase transition, StringBuilder mdContent, string indent, string sourceStateName)
    {
        string destinationName = GetDestinationStateName(transition);
        
        mdContent.AppendLine($"\n{indent}- **过渡**: {sourceStateName} → {destinationName}");
        mdContent.AppendLine($"{indent}  - **是否退出**: {transition.isExit}");
        
        // 修复：根据具体类型获取属性
        if (transition is AnimatorStateTransition)
        {
            var stateTransition = transition as AnimatorStateTransition;
            mdContent.AppendLine($"{indent}  - **持续时间**: {stateTransition.duration}");
            mdContent.AppendLine($"{indent}  - **偏移**: {stateTransition.offset}");
            mdContent.AppendLine($"{indent}  - **打断源**: {stateTransition.interruptionSource}");
            mdContent.AppendLine($"{indent}  - **有序打断**: {stateTransition.orderedInterruption}");
            mdContent.AppendLine($"{indent}  - **退出时间**: {stateTransition.hasExitTime}");
            
            if (stateTransition.hasExitTime)
            {
                mdContent.AppendLine($"{indent}  - **退出时间值**: {stateTransition.exitTime}");
                mdContent.AppendLine($"{indent}  - **退出时间固定时长**: {stateTransition.hasFixedDuration}");
            }
        }
        else if (transition is AnimatorTransition)
        {
            var animatorTransition = transition as AnimatorTransition;
            // AnimatorTransition没有duration等属性
        }

        // 导出Conditions
        var conditions = GetTransitionConditions(transition);
        if (conditions.Count > 0)
        {
            mdContent.AppendLine($"{indent}  - **条件**:");
            for (int i = 0; i < conditions.Count; i++)
            {
                mdContent.AppendLine($"{indent}    - {conditions[i]}");
            }
        }
        else
        {
            mdContent.AppendLine($"{indent}  - **条件**: 无条件过渡");
        }
    }

    // 新增：获取目标状态名称
    private string GetDestinationStateName(AnimatorTransitionBase transition)
    {
        if (transition is AnimatorTransition)
        {
            var animatorTransition = transition as AnimatorTransition;
            if (animatorTransition.destinationState != null)
                return animatorTransition.destinationState.name;
            else if (animatorTransition.destinationStateMachine != null)
                return animatorTransition.destinationStateMachine.name;
        }
        else if (transition is AnimatorStateTransition)
        {
            var stateTransition = transition as AnimatorStateTransition;
            if (stateTransition.destinationState != null)
                return stateTransition.destinationState.name;
            else if (stateTransition.destinationStateMachine != null)
                return stateTransition.destinationStateMachine.name;
        }
        
        return "Unknown";
    }

    // 新增：获取Transition的所有条件
    private List<string> GetTransitionConditions(AnimatorTransitionBase transition)
    {
        var conditions = new List<string>();
        
        if (transition is AnimatorTransition)
        {
            var animatorTransition = transition as AnimatorTransition;
            for (int i = 0; i < animatorTransition.conditions.Length; i++)
            {
                conditions.Add(FormatCondition(animatorTransition.conditions[i]));
            }
        }
        else if (transition is AnimatorStateTransition)
        {
            var stateTransition = transition as AnimatorStateTransition;
            for (int i = 0; i < stateTransition.conditions.Length; i++)
            {
                conditions.Add(FormatCondition(stateTransition.conditions[i]));
            }
        }
        
        return conditions;
    }

    // 新增：格式化条件显示
    private string FormatCondition(AnimatorCondition condition)
    {
        string mode = GetConditionModeDisplay(condition.mode);
        string parameter = condition.parameter;
        string threshold = condition.threshold.ToString("F2");
        
        return $"{parameter} {mode} {threshold}";
    }

    // 新增：获取条件模式的显示文本
    private string GetConditionModeDisplay(AnimatorConditionMode mode)
    {
        switch (mode)
        {
            case AnimatorConditionMode.If: return "==";
            case AnimatorConditionMode.IfNot: return "!=";
            case AnimatorConditionMode.Greater: return ">";
            case AnimatorConditionMode.Less: return "<";
            case AnimatorConditionMode.Equals: return "==";
            case AnimatorConditionMode.NotEqual: return "!=";
            default: return mode.ToString();
        }
    }

    private Dictionary<string, string> GetSelectedParameters(StateMachineBehaviour behaviour)
    {
        var result = new Dictionary<string, string>();
        
        if (behaviour == null) return result;
        
        var keys = new List<string>(parameterSelection.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            string selectionKey = keys[i];
            if (!parameterSelection[selectionKey]) continue;
            
            string paramName = selectionKey.Substring(selectionKey.IndexOf(":") + 2);
            
            try
            {
                object value = null;
                
                if (selectionKey.StartsWith("字段:"))
                {
                    var field = behaviour.GetType().GetField(paramName);
                    if (field != null)
                    {
                        value = field.GetValue(behaviour);
                    }
                }
                else if (selectionKey.StartsWith("属性:"))
                {
                    var property = behaviour.GetType().GetProperty(paramName);
                    if (property != null && property.CanRead)
                    {
                        value = property.GetValue(behaviour);
                    }
                }
                
                if (value != null)
                {
                    result[selectionKey] = value.ToString();
                }
                else
                {
                    result[selectionKey] = "null";
                }
            }
            catch (Exception e)
            {
                result[selectionKey] = $"<读取错误: {e.Message}>";
            }
        }
        
        return result;
    }

    [MenuItem("Assets/导出状态机配置报告", true)]
    public static bool ValidateExportAnimatorConfig()
    {
        return Selection.activeObject is AnimatorController;
    }
}