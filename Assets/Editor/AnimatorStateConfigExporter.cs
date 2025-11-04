using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;

public class AnimatorStateConfigExporter : EditorWindow
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

    [MenuItem("Assets/导出状态机配置报告", false, 30)]
    public static void ExportAnimatorConfig()
    {
        var selectedObject = Selection.activeObject;
        
        if (selectedObject == null || !(selectedObject is AnimatorController))
        {
            EditorUtility.DisplayDialog("错误", "请选择一个Animator Controller资产！", "确定");
            return;
        }

        var window = CreateInstance<AnimatorStateConfigExporter>();
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
        if (selectedBehaviourType != null && parameterSelection.Count > 0)
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

    // 修复：使用for循环遍历索引来避免集合修改错误
    private void DrawParameterSelectionPanel()
    {
        GUILayout.Label("选择要导出的参数:", EditorStyles.boldLabel);
        
        // 全选/取消全选按钮
        EditorGUILayout.BeginHorizontal();
        bool newSelectAll = EditorGUILayout.Toggle("全选", selectAllParameters);
        if (newSelectAll != selectAllParameters)
        {
            selectAllParameters = newSelectAll;
            // 修复：使用Keys.ToList()创建副本进行遍历
            var keys = new List<string>(parameterSelection.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                parameterSelection[keys[i]] = selectAllParameters;
            }
        }
        
        if (GUILayout.Button("反选", GUILayout.Width(60)))
        {
            // 修复：使用for循环遍历索引
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
        
        // 修复关键：使用for循环遍历索引而不是直接枚举字典
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

    // 更新参数列表
    private void UpdateParameterList()
    {
        parameterSelection.Clear();
        
        if (selectedBehaviourType != null)
        {
            // 获取公共字段
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
            
            // 获取公共属性
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

    // 获取参数类型显示信息
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

    // 更新全选状态
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

    // 获取已选择的参数数量
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

            // 遍历所有Layer
            for (int layerIndex = 0; layerIndex < selectedController.layers.Length; layerIndex++)
            {
                var layer = selectedController.layers[layerIndex];
                mdContent.AppendLine($"\n* LayerName: {layer.name}");

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

    private void ProcessStateMachine(AnimatorStateMachine stateMachine, StringBuilder mdContent, string indent, int layerIndex)
    {
        if (stateMachine == null) return;

        // 处理当前状态机中的所有状态
        for (int i = 0; i < stateMachine.states.Length; i++)
        {
            var childState = stateMachine.states[i];
            AnimatorState state = childState.state;
            mdContent.AppendLine($"{indent}  + 动画状态: {state.name}");

            StateMachineBehaviour[] behaviours = state.behaviours;
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

            if (targetBehaviours.Count > 0)
            {
                for (int k = 0; k < targetBehaviours.Count; k++)
                {
                    var behaviour = targetBehaviours[k];
                    string behaviourName = behaviour.GetType().Name;
                    mdContent.AppendLine($"{indent}    - Component: {behaviourName}");
                    
                    // 只导出用户选择的参数
                    var selectedParams = GetSelectedParameters(behaviour);
                    
                    var paramKeys = new List<string>(selectedParams.Keys);
                    for (int m = 0; m < paramKeys.Count; m++)
                    {
                        string key = paramKeys[m];
                        mdContent.AppendLine($"{indent}      - {key}: {selectedParams[key]}");
                    }
                    
                    if (selectedParams.Count == 0)
                    {
                        mdContent.AppendLine($"{indent}  - Empty");
                    }
                }
            }
            else
            {
                mdContent.AppendLine($"{indent}  - 未找到指定类型的组件");
            }
            
            mdContent.AppendLine();
        }

        // 递归处理子状态机
        for (int i = 0; i < stateMachine.stateMachines.Length; i++)
        {
            var childStateMachine = stateMachine.stateMachines[i];
            mdContent.AppendLine($"{indent}  + 子状态机: {childStateMachine.stateMachine.name}");
            ProcessStateMachine(childStateMachine.stateMachine, mdContent, indent + "  ", layerIndex);
        }
    }

    // 获取用户选择的参数
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