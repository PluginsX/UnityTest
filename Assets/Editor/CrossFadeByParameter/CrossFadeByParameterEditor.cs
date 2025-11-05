using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;
using System.Collections.Generic;
using System;

[CustomEditor(typeof(CrossFadeByParameter))]
public class CrossFadeByParameterEditor : Editor
{
    private SerializedProperty nextStateName;
    private SerializedProperty hasExitTime;
    private SerializedProperty exitTime;
    private SerializedProperty useFixedDuration;
    private SerializedProperty transitionOffset;
    private SerializedProperty transitionDuration;
    private SerializedProperty blendCurve;
    private SerializedProperty canBeInterrupted;
    private SerializedProperty conditions;

    private AnimatorController targetAnimatorController;
    private List<string> availableParameters = new List<string>();
    private bool isInitialized = false;

    // 获取当前State所在的Animator Controller（完整封装版本）
    private AnimatorController GetAnimatorController()
    {
        // 清除之前的引用
        targetAnimatorController = null;
        
        // 检查target是否为StateMachineBehaviour
        if (!(target is StateMachineBehaviour behaviour))
        {
            Debug.Log("Target不是StateMachineBehaviour类型");
            return null;
        }
        
        try
        {
            // 方法1：通过序列化属性直接获取（最直接的方式）
            SerializedObject serializedBehaviour = new SerializedObject(behaviour);
            SerializedProperty controllerProp = serializedBehaviour.FindProperty("m_Controller");
            
            if (controllerProp != null)
            {
                targetAnimatorController = controllerProp.objectReferenceValue as AnimatorController;
                if (targetAnimatorController != null)
                {
                    Debug.Log($"直接获取到Animator Controller: {targetAnimatorController.name}");
                    return targetAnimatorController;
                }
            }
            
            // 方法2：通过State获取（备用方案）
            SerializedProperty stateProp = serializedBehaviour.FindProperty("m_State");
            if (stateProp != null)
            {
                AnimatorState state = stateProp.objectReferenceValue as AnimatorState;
                if (state != null)
                {
                    // 查找包含此State的StateMachine
                    AnimatorStateMachine stateMachine = FindParentStateMachine(state);
                    if (stateMachine != null)
                    {
                        // 查找包含此StateMachine的Controller
                        targetAnimatorController = FindParentController(stateMachine);
                        if (targetAnimatorController != null)
                        {
                            Debug.Log($"✅ 通过State层级获取到Animator Controller: {targetAnimatorController.name}");
                            return targetAnimatorController;
                        }
                    }
                }
            }
            
            // 方法3：通过反射获取（最后的手段）
            try
            {
                var controllerField = typeof(StateMachineBehaviour).GetField("m_Controller", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (controllerField != null)
                {
                    targetAnimatorController = controllerField.GetValue(behaviour) as AnimatorController;
                    if (targetAnimatorController != null)
                    {
                        Debug.Log($"✅ 通过反射获取到Animator Controller: {targetAnimatorController.name}");
                        return targetAnimatorController;
                    }
                }
            }
            catch (Exception reflectionEx)
            {
                Debug.LogWarning($"反射获取Controller失败: {reflectionEx.Message}");
            }
            
            // 方法4：在项目中查找（兜底方案）
            Debug.Log("⚠️ 尝试在项目中查找Animator Controller");
            string[] guids = AssetDatabase.FindAssets("t:AnimatorController");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                targetAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (targetAnimatorController != null)
                {
                    Debug.Log($"✅ 从项目获取Animator Controller: {targetAnimatorController.name}");
                    return targetAnimatorController;
                }
            }
            
            Debug.LogWarning("❌ 无法获取State所在的Animator Controller");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"获取Animator Controller时发生错误: {e.Message}");
            return null;
        }
    }

    // 查找包含指定State的父级StateMachine（内部辅助方法）
    private AnimatorStateMachine FindParentStateMachine(AnimatorState targetState)
    {
        if (targetState == null) return null;
        
        // 查找所有StateMachine
        string[] stateMachineGUIDs = AssetDatabase.FindAssets("t:AnimatorStateMachine");
        foreach (string guid in stateMachineGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimatorStateMachine stateMachine = AssetDatabase.LoadAssetAtPath<AnimatorStateMachine>(path);
            if (stateMachine != null && ContainsStateRecursive(stateMachine, targetState))
            {
                return stateMachine;
            }
        }
        
        return null;
    }

    // 递归检查StateMachine是否包含指定的State（内部辅助方法）
    private bool ContainsStateRecursive(AnimatorStateMachine stateMachine, AnimatorState targetState)
    {
        if (stateMachine == null || targetState == null) return false;
        
        // 检查当前StateMachine的状态
        foreach (ChildAnimatorState state in stateMachine.states)
        {
            if (state.state == targetState)
                return true;
        }
        
        // 递归检查子StateMachine
        foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
        {
            if (ContainsStateRecursive(childMachine.stateMachine, targetState))
                return true;
        }
        
        return false;
    }

    // 查找包含指定StateMachine的父级Controller（内部辅助方法）
    private AnimatorController FindParentController(AnimatorStateMachine targetStateMachine)
    {
        if (targetStateMachine == null) return null;
        
        // 查找所有Controller
        string[] controllerGUIDs = AssetDatabase.FindAssets("t:AnimatorController");
        foreach (string guid in controllerGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null && ContainsStateMachineRecursive(controller, targetStateMachine))
            {
                return controller;
            }
        }
        
        return null;
    }

    // 递归检查Controller是否包含指定的StateMachine（内部辅助方法）
    private bool ContainsStateMachineRecursive(AnimatorController controller, AnimatorStateMachine targetStateMachine)
    {
        if (controller == null || targetStateMachine == null) return false;
        
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            if (layer.stateMachine == targetStateMachine)
                return true;
            
            if (ContainsStateMachineInChildren(layer.stateMachine, targetStateMachine))
                return true;
        }
        
        return false;
    }

    // 递归检查StateMachine的子级（内部辅助方法）
    private bool ContainsStateMachineInChildren(AnimatorStateMachine parent, AnimatorStateMachine target)
    {
        if (parent == null || target == null) return false;
        
        foreach (ChildAnimatorStateMachine child in parent.stateMachines)
        {
            if (child.stateMachine == target)
                return true;
            
            if (ContainsStateMachineInChildren(child.stateMachine, target))
                return true;
        }
        
        return false;
    }

    // 启用时
    private void OnEnable()
    {
        // 获取关联的Animator信息
        GetAnimatorController();

        // 修复：检查目标对象是否有效
        if (targetAnimatorController == null)
        {
            Debug.LogWarning("获取AnimatorController失败");
            return;
        }
        try
        {
            // 延迟初始化，确保序列化对象可用
            EditorApplication.delayCall += InitializeEditor;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"初始化CrossFadeByParameterEditor失败。: {e.Message}");
        }


    }
 
    // 初始化编辑器
    private void InitializeEditor()
    {
        if (isInitialized) return;
        
        try
        {
            // 修复：安全地获取序列化属性
            serializedObject.Update();
            
            nextStateName = serializedObject.FindProperty("nextStateName");
            hasExitTime = serializedObject.FindProperty("hasExitTime");
            exitTime = serializedObject.FindProperty("exitTime");
            useFixedDuration = serializedObject.FindProperty("useFixedDuration");
            transitionOffset = serializedObject.FindProperty("transitionOffset");
            transitionDuration = serializedObject.FindProperty("transitionDuration");
            blendCurve = serializedObject.FindProperty("blendCurve");
            canBeInterrupted = serializedObject.FindProperty("canBeInterrupted");
            conditions = serializedObject.FindProperty("conditions");

            // 验证属性是否成功获取
            if (nextStateName == null || hasExitTime == null)
            {
                Debug.LogError("未能找到所需的序列化属性");
                return;
            }

            GetAnimatorController();
            UpdateAvailableParameters();
            
            isInitialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"初始化编辑器错误: {e.Message}");
        }
    }

    // 销毁组件时
    private void OnDisable()
    {
        // 清理延迟调用
        EditorApplication.delayCall -= InitializeEditor;
    }


    // 更新当前控制器中可用参数库（优化版本）
    private void UpdateAvailableParameters()
    {
        // 清空参数列表
        availableParameters.Clear();
        availableParameters.Add(""); // 空选项

        // 确保有有效的Controller
        if (targetAnimatorController == null)
        {
            targetAnimatorController = GetAnimatorController();
        }

        // 如果获取到了Controller
        if (targetAnimatorController != null)
        {
            try
            {
                // 检查参数数组是否有效
                if (targetAnimatorController.parameters == null)
                {
                    Debug.LogWarning($"控制器 '{targetAnimatorController.name}' 的参数数组为null");
                    AddSampleParameters();
                    return;
                }

                int paramCount = targetAnimatorController.parameters.Length;
                
                if (paramCount == 0)
                {
                    Debug.LogWarning($"控制器 '{targetAnimatorController.name}' 没有任何参数");
                    AddSampleParameters();
                    return;
                }

                // 使用for循环避免枚举器问题，并添加验证
                int addedCount = 0;
                for (int i = 0; i < paramCount; i++)
                {
                    var param = targetAnimatorController.parameters[i];
                    
                    // 验证参数有效性
                    if (IsValidParameter(param))
                    {
                        if (!availableParameters.Contains(param.name))
                        {
                            availableParameters.Add(param.name);
                            addedCount++;
                        }
                        else
                        {
                            Debug.LogWarning($"发现重复参数名: {param.name}，已跳过");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"跳过无效参数: 索引={i}, 名称='{param.name}'");
                    }
                }

                if (addedCount > 0)
                {
                    Debug.Log($"成功加载 {addedCount}/{paramCount} 个参数 from '{targetAnimatorController.name}'");
                    
                    // 按字母顺序排序（可选）
                    availableParameters.Sort();
                }
                else
                {
                    Debug.LogWarning($"控制器 '{targetAnimatorController.name}' 没有有效参数，使用示例参数");
                    AddSampleParameters();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"从控制器 '{targetAnimatorController.name}' 获取参数失败: {e.Message}");
                Debug.LogException(e);
                
                // 失败时添加示例参数
                AddSampleParameters();
            }
        }
        else
        {
            Debug.LogWarning("无法获取Animator Controller引用，使用示例参数");
            
            // 添加示例参数作为备用
            AddSampleParameters();
        }
    }

    // 验证参数有效性
    private bool IsValidParameter(AnimatorControllerParameter param)
    {
        if (param == null)
            return false;
        
        if (string.IsNullOrEmpty(param.name))
            return false;
        
        if (param.name.Trim().Length == 0)
            return false;
        
        // 检查参数名是否包含非法字符
        if (ContainsInvalidCharacters(param.name))
            return false;
        
        return true;
    }

    // 检查非法字符
    private bool ContainsInvalidCharacters(string paramName)
    {
        char[] invalidChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        return paramName.IndexOfAny(invalidChars) >= 0;
    }

    // 添加示例参数
    private void AddSampleParameters()
    {
        // 添加常用示例参数
        string[] sampleParams = { 
            "Speed", "IsGrounded", "Attack", "Jump", "Health", 
            "Stamina", "Direction", "IsMoving", "IsCrouching", "IsFalling",
            "IsDead", "IsAttacking", "IsJumping", "VelocityX", "VelocityY",
            "Trigger_Attack", "Trigger_Jump", "Trigger_Hit", "Trigger_Death"
        };
        
        int addedCount = 0;
        foreach (var param in sampleParams)
        {
            if (!string.IsNullOrEmpty(param) && !availableParameters.Contains(param))
            {
                availableParameters.Add(param);
                addedCount++;
            }
        }
        
        if (addedCount > 0)
        {
            Debug.Log($"添加了 {addedCount} 个示例参数");
        }
    }

    // 获取参数详细信息（用于调试）
    private void LogParameterDetails()
    {
        if (targetAnimatorController == null || targetAnimatorController.parameters == null)
            return;
        
        Debug.Log($"控制器 '{targetAnimatorController.name}' 参数详情:");
        for (int i = 0; i < targetAnimatorController.parameters.Length; i++)
        {
            var param = targetAnimatorController.parameters[i];
            Debug.Log($"  [{i}] {param.name} ({param.type}) - Default: {GetParameterDefaultValue(param)}");
        }
    }

    // 获取参数默认值
    private string GetParameterDefaultValue(AnimatorControllerParameter param)
    {
        if (param == null) return "null";
        
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

    // 检查参数是否存在
    public bool HasParameter(string parameterName)
    {
        if (targetAnimatorController == null || string.IsNullOrEmpty(parameterName))
            return false;
        
        try
        {
            for (int i = 0; i < targetAnimatorController.parameters.Length; i++)
            {
                if (targetAnimatorController.parameters[i].name == parameterName)
                    return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"检查参数存在性失败: {e.Message}");
        }
        
        return false;
    }

    // 绘制inspector UI
    public override void OnInspectorGUI()
    {
        // 修复：检查初始化状态
        if (!isInitialized)
        {
            EditorGUILayout.HelpBox("编辑器未正确初始化。请重新选择对象。", MessageType.Error);
            if (GUILayout.Button("重试初始化"))
            {
                InitializeEditor();
            }
            return;
        }

        if (serializedObject == null || target == null)
        {
            EditorGUILayout.HelpBox("序列化对象为空。该组件可能已被删除。", MessageType.Error);
            return;
        }

        serializedObject.Update();
        
        EditorGUI.BeginChangeCheck();
        
        try
        {
            // 1. 设置区域
            DrawSettingsSection();
            // 3. 混合设置区域
            DrawMixSettingsSection();
            // 4. 转换控制
            DrawTransitionControlSection();
            // 2. 条件区域
            DrawConditionsSection();
        }
        catch (System.Exception e)
        {
            EditorGUILayout.HelpBox($"绘制UI错误: {e.Message}", MessageType.Error);
        }
        
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
        }

        // 刷新按钮
        EditorGUILayout.Space();
        if (GUILayout.Button("刷新参数"))
        {
            GetAnimatorController();
            UpdateAvailableParameters();
            Repaint();
        }
    }

    private void DrawSettingsSection()
    {
        if (nextStateName == null || hasExitTime == null)
        {
            EditorGUILayout.EndVertical();
            return;
        }
        
        EditorGUILayout.LabelField("过渡设置", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical(GUI.skin.box);
        {
            EditorGUILayout.PropertyField(nextStateName, new GUIContent("目标状态名"));
            EditorGUILayout.PropertyField(hasExitTime, GUILayout.Width(120));


            if (hasExitTime.boolValue && useFixedDuration != null)
            {
                EditorGUILayout.PropertyField(useFixedDuration, new GUIContent("固定时间"));
            }
            
            EditorGUILayout.BeginHorizontal();
            if (hasExitTime.boolValue)
            {
                EditorGUILayout.PropertyField(exitTime, new GUIContent("混出位置"));
                EditorGUILayout.LabelField(useFixedDuration.boolValue ? "s" : "%", GUILayout.Width(15));
            }
            EditorGUILayout.EndHorizontal();
            
            
        }
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space();
    }

    private void DrawConditionsSection()
    {
        if (conditions == null) return;
        
        EditorGUILayout.LabelField("转换条件", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical(GUI.skin.box);
        {
            if (targetAnimatorController != null)
            {
                EditorGUILayout.LabelField($"使用: {targetAnimatorController.name}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.HelpBox("没有找到动画控制器。", MessageType.Warning);
            }
            
            if (conditions.arraySize == 0)
            {
                EditorGUILayout.HelpBox("无条件", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < conditions.arraySize; i++)
                {
                    var condition = conditions.GetArrayElementAtIndex(i);
                    if (condition != null)
                    {
                        DrawConditionRow(condition, i);
                        // 每行上下间隔
                        // if (i < conditions.arraySize - 1)
                        // {
                        //     EditorGUILayout.Separator();
                        // }
                    }
                }
            }
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                conditions.arraySize++;
                serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space();
    }

    // 绘制一行条件
    private void DrawConditionRow(SerializedProperty condition, int index)
    {
        if (condition == null) return;
        
        SerializedProperty parameterProp = condition.FindPropertyRelative("parameter");
        SerializedProperty modeProp = condition.FindPropertyRelative("mode");
        SerializedProperty thresholdProp = condition.FindPropertyRelative("threshold");
        
        if (parameterProp == null || modeProp == null || thresholdProp == null) return;
        
        EditorGUILayout.BeginHorizontal();
        {
            DrawParameterField(parameterProp);
            
            // 获取参数名称和类型
            string parameterName = parameterProp.stringValue ?? "";
            AnimatorControllerParameterType paramType = GetParameterType(parameterName);
            
            // 根据参数类型决定绘制内容
            DrawModeField(condition,paramType);
            
            DrawThresholdField(thresholdProp, parameterName);

            // 添加FlexibleSpace让按钮靠右
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("-", GUILayout.Width(20)))
            {
                conditions.DeleteArrayElementAtIndex(index);
                return;
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    // 检查属性是否为枚举类型
    private bool IsEnumProperty(SerializedProperty prop)
    {
        return prop != null && prop.propertyType == SerializedPropertyType.Enum;
    }

    // 绘制模式字段（根据您的枚举定义修改）
    private void DrawModeField(SerializedProperty modeProp, AnimatorControllerParameterType paramType)
    {

        if (modeProp == null || !IsEnumProperty(modeProp)) return;
        
        // 确保枚举值在有效范围内（0-3对应您的枚举值）
        int enumCount = Enum.GetValues(typeof(CompareOperator)).Length;
        if (modeProp.enumValueIndex < 0 || modeProp.enumValueIndex >= enumCount)
        {
            modeProp.enumValueIndex = (int)CompareOperator.Equals;
        }
        
        switch (paramType)
        {
            case AnimatorControllerParameterType.Trigger:
                return; // 不绘制
                
            case AnimatorControllerParameterType.Bool:
                DrawBoolModeField(modeProp);
                break;
                
            case AnimatorControllerParameterType.Float:
            case AnimatorControllerParameterType.Int:
                DrawNumericModeField(modeProp);
                break;
                
            default:
                DrawDefaultModeField(modeProp);
                break;
        }
    }

    // Bool类型的模式选择（根据您的枚举定义修改）
    private void DrawBoolModeField(SerializedProperty modeProp)
    {
        if (modeProp == null || !IsEnumProperty(modeProp)) return;
        
        string[] boolModeSymbols = { "==" };
        
        // 强制设置为等于操作符（根据您的枚举定义）
        if (modeProp.enumValueIndex != (int)CompareOperator.Equals)
        {
            modeProp.enumValueIndex = (int)CompareOperator.Equals;
        }
        
        // 显示只读的等于操作符
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.Popup(0, boolModeSymbols, GUILayout.Width(50));
        EditorGUI.EndDisabledGroup();
    }

    // 数值类型的模式选择（根据您的枚举定义修改）
    private void DrawNumericModeField(SerializedProperty modeProp)
    {
        if (modeProp == null || !IsEnumProperty(modeProp)) return;
        
        string[] numericModeSymbols = { ">", "<", "==", "!=" };
        
        // 确保选择的是有效模式（0-3对应您的枚举值）
        int selectedMode = Mathf.Clamp(modeProp.enumValueIndex, 0, numericModeSymbols.Length - 1);
        
        int newMode = EditorGUILayout.Popup(selectedMode, numericModeSymbols, GUILayout.Width(50));
        
        if (newMode != selectedMode && newMode >= 0 && newMode < numericModeSymbols.Length)
        {
            modeProp.enumValueIndex = newMode;
        }
    }

    // 默认模式选择（根据您的枚举定义修改）
    private void DrawDefaultModeField(SerializedProperty modeProp)
    {
        if (modeProp == null || !IsEnumProperty(modeProp)) return;
        
        string[] modeSymbols = { ">", "<", "==", "!=" };
        
        // 确保选择的是有效模式（0-3对应您的枚举值）
        int selectedMode = Mathf.Clamp(modeProp.enumValueIndex, 0, modeSymbols.Length - 1);
        
        int newMode = EditorGUILayout.Popup(selectedMode, modeSymbols, GUILayout.Width(50));
        
        if (newMode != selectedMode && newMode >= 0 && newMode < modeSymbols.Length)
        {
            modeProp.enumValueIndex = newMode;
        }
    }

    // 获取操作符符号（根据您的枚举定义修改）
    private string GetOperatorSymbol(CompareOperator op)
    {
        switch (op)
        {
            case CompareOperator.Greater: return ">";
            case CompareOperator.Less: return "<";
            case CompareOperator.Equals: return "==";
            case CompareOperator.NotEqual: return "!=";
            default: return "==";
        }
    }

    // 绘制参数选择字段
    private void DrawParameterField(SerializedProperty parameterProp)
    {
        if (parameterProp == null || availableParameters == null) return;
        
        int selectedIndex = 0;
        string currentParam = parameterProp.stringValue ?? "";
        
        // 查找当前选择的参数索引
        for (int i = 0; i < availableParameters.Count; i++)
        {
            if (availableParameters[i] == currentParam)
            {
                selectedIndex = i;
                break;
            }
        }
        
        // 绘制参数下拉选择框
        int newIndex = EditorGUILayout.Popup(selectedIndex, availableParameters.ToArray(), GUILayout.Width(120));
        
        if (newIndex >= 0 && newIndex < availableParameters.Count)
        {
            parameterProp.stringValue = availableParameters[newIndex];
        }
    }
    // 获取参数类型
    private AnimatorControllerParameterType GetParameterType(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName) || targetAnimatorController == null)
            return AnimatorControllerParameterType.Float;
        
        try
        {
            for (int i = 0; i < targetAnimatorController.parameters.Length; i++)
            {
                var param = targetAnimatorController.parameters[i];
                if (param.name == parameterName)
                    return param.type;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"获取参数类型失败: {e.Message}");
        }
        
        return AnimatorControllerParameterType.Float;
    }

    

    private void DrawThresholdField(SerializedProperty thresholdProp, string parameterName)
    {
        if (thresholdProp == null) return;
        
        if (string.IsNullOrEmpty(parameterName))
        {
            EditorGUILayout.PropertyField(thresholdProp, GUIContent.none);
            return;
        }
        
        try
        {
            var paramType = GetParameterType(parameterName);//
            switch (paramType)
            {
                case AnimatorControllerParameterType.Float:
                    thresholdProp.floatValue = EditorGUILayout.FloatField(thresholdProp.floatValue);
                    break;
                case AnimatorControllerParameterType.Int:
                    thresholdProp.floatValue = EditorGUILayout.IntField((int)thresholdProp.floatValue);
                    break;
                case AnimatorControllerParameterType.Bool:
                    bool currentBool = thresholdProp.floatValue > 0.5f;
                    bool newBool = EditorGUILayout.Toggle(currentBool);
                    thresholdProp.floatValue = newBool ? 1f : 0f;
                    break;
                case AnimatorControllerParameterType.Trigger:
                    EditorGUILayout.LabelField("(Trigger)", GUILayout.Width(60));
                    break;
                default:
                    EditorGUILayout.PropertyField(thresholdProp, GUIContent.none);
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"错误绘制阈值字段: {e.Message}");
            EditorGUILayout.PropertyField(thresholdProp, GUIContent.none);
        }
    }

    private void DrawMixSettingsSection()
    {
        if (transitionOffset == null || transitionDuration == null || blendCurve == null) return;
        
        EditorGUILayout.LabelField("混合设置", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical(GUI.skin.box);
        {
            EditorGUILayout.PropertyField(transitionOffset, new GUIContent("过渡偏移"));
            EditorGUILayout.PropertyField(transitionDuration, new GUIContent("过度时间"));
            EditorGUILayout.PropertyField(blendCurve, new GUIContent("混合曲线"));
        }
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space();
    }

    private void DrawTransitionControlSection()
    {
        if (canBeInterrupted == null) return;
        
        EditorGUILayout.LabelField("过渡控制", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical(GUI.skin.box);
        {
            EditorGUILayout.PropertyField(canBeInterrupted, new GUIContent("允许打断"));
        }
        EditorGUILayout.EndVertical();
    }
}