using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;
using System.Collections.Generic;

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
    
    private AnimatorController animatorController;
    private List<string> availableParameters = new List<string>();
    private bool isInitialized = false;

    private void OnEnable()
    {
        // 修复：检查目标对象是否有效
        if (target == null)
        {
            Debug.LogWarning("CrossFadeByParameterEditor: Target object is null");
            return;
        }
        try
        {
            // 延迟初始化，确保序列化对象可用
            EditorApplication.delayCall += InitializeEditor;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to initialize CrossFadeByParameterEditor: {e.Message}");
        }
    }
 
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
                Debug.LogError("Failed to find required serialized properties");
                return;
            }

            UpdateAnimatorController();
            UpdateAvailableParameters();
            
            isInitialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in InitializeEditor: {e.Message}");
        }
    }

    private void OnDisable()
    {
        // 清理延迟调用
        EditorApplication.delayCall -= InitializeEditor;
    }

    private void UpdateAnimatorController()
    {
        animatorController = null;
        availableParameters.Clear();
        availableParameters.Add(""); // 空选项

        try
        {
            // 方法1：从当前选中的对象获取
            var selectedObject = Selection.activeObject;
            if (selectedObject is AnimatorController controller)
            {
                animatorController = controller;
            }
            else if (selectedObject is GameObject gameObject)
            {
                var animator = gameObject.GetComponent<Animator>();
                if (animator != null && animator.runtimeAnimatorController is AnimatorController runtimeController)
                {
                    animatorController = runtimeController;
                }
            }

            // 方法2：从AssetDatabase查找
            if (animatorController == null)
            {
                string[] controllerGUIDs = AssetDatabase.FindAssets("t:AnimatorController");
                foreach (string guid in controllerGUIDs)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AnimatorController controllerAsset = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                    if (controllerAsset != null)
                    {
                        animatorController = controllerAsset;
                        break;
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to update AnimatorController: {e.Message}");
        }
    }

    private void UpdateAvailableParameters()
    {
        availableParameters.Clear();
        availableParameters.Add(""); // 空选项
        
        if (animatorController != null)
        {
            try
            {
                foreach (var param in animatorController.parameters)
                {
                    availableParameters.Add(param.name);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to get parameters from AnimatorController: {e.Message}");
            }
        }
        
        // 添加示例参数（如果无法获取真实参数）
        if (availableParameters.Count <= 1)
        {
            availableParameters.AddRange(new string[] { "Speed", "IsGrounded", "Attack", "Jump", "Health" });
        }
    }

    public override void OnInspectorGUI()
    {
        // 修复：检查初始化状态
        if (!isInitialized)
        {
            EditorGUILayout.HelpBox("Editor not initialized properly. Please reselect the object.", MessageType.Error);
            if (GUILayout.Button("Try Initialize Again"))
            {
                InitializeEditor();
            }
            return;
        }

        if (serializedObject == null || target == null)
        {
            EditorGUILayout.HelpBox("Serialized object is null. The component may have been deleted.", MessageType.Error);
            return;
        }

        serializedObject.Update();
        
        EditorGUI.BeginChangeCheck();
        
        try
        {
            // 1. 设置区域
            DrawSettingsSection();
            
            // 2. 条件区域
            DrawConditionsSection();
            
            // 3. 混合设置区域
            DrawMixSettingsSection();
            
            // 4. 转换控制
            DrawTransitionControlSection();
        }
        catch (System.Exception e)
        {
            EditorGUILayout.HelpBox($"Error drawing UI: {e.Message}", MessageType.Error);
        }
        
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
        }

        // 刷新按钮
        EditorGUILayout.Space();
        if (GUILayout.Button("Refresh Parameters"))
        {
            UpdateAnimatorController();
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
        
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical(GUI.skin.box);
        {
            EditorGUILayout.PropertyField(nextStateName);
            EditorGUILayout.PropertyField(hasExitTime, GUILayout.Width(120));


            if (hasExitTime.boolValue && useFixedDuration != null)
            {
                EditorGUILayout.PropertyField(useFixedDuration, new GUIContent("Fixed Duration"));
            }
            
            EditorGUILayout.BeginHorizontal();
            if (hasExitTime.boolValue)
            {
                EditorGUILayout.PropertyField(exitTime, new GUIContent("ExitTime"));
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
        
        EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical(GUI.skin.box);
        {
            if (animatorController != null)
            {
                EditorGUILayout.LabelField($"Using: {animatorController.name}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.HelpBox("No Animator Controller found. Using sample parameters.", MessageType.Warning);
            }
            
            if (conditions.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No conditions", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < conditions.arraySize; i++)
                {
                    var condition = conditions.GetArrayElementAtIndex(i);
                    if (condition != null)
                    {
                        DrawConditionRow(condition, i);
                        if (i < conditions.arraySize - 1)
                        {
                            EditorGUILayout.Separator();
                        }
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
            DrawModeField(modeProp);
            DrawThresholdField(thresholdProp, parameterProp.stringValue);
            
            if (GUILayout.Button("-", GUILayout.Width(20)))
            {
                conditions.DeleteArrayElementAtIndex(index);
                return;
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawParameterField(SerializedProperty parameterProp)
    {
        if (parameterProp == null || availableParameters == null) return;
        
        int selectedIndex = 0;
        string currentParam = parameterProp.stringValue ?? "";
        
        for (int i = 0; i < availableParameters.Count; i++)
        {
            if (availableParameters[i] == currentParam)
            {
                selectedIndex = i;
                break;
            }
        }
        
        int newIndex = EditorGUILayout.Popup(selectedIndex, availableParameters.ToArray(), GUILayout.Width(120));
        if (newIndex >= 0 && newIndex < availableParameters.Count)
        {
            parameterProp.stringValue = availableParameters[newIndex];
        }
    }

    private void DrawModeField(SerializedProperty modeProp)
    {
        if (modeProp == null) return;
        
        string[] modeSymbols = { ">", "<", "==", "!=" };
        int selectedMode = modeProp.enumValueIndex;
        int newMode = EditorGUILayout.Popup(selectedMode, modeSymbols, GUILayout.Width(50));
        
        if (newMode != selectedMode && newMode >= 0 && newMode < modeSymbols.Length)
        {
            modeProp.enumValueIndex = newMode;
        }
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
            var paramType = GetParameterType(parameterName);
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
            Debug.LogWarning($"Error drawing threshold field: {e.Message}");
            EditorGUILayout.PropertyField(thresholdProp, GUIContent.none);
        }
    }

    private AnimatorControllerParameterType GetParameterType(string parameterName)
    {
        if (animatorController == null || string.IsNullOrEmpty(parameterName))
            return AnimatorControllerParameterType.Float;
        
        try
        {
            foreach (var param in animatorController.parameters)
            {
                if (param.name == parameterName)
                    return param.type;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error getting parameter type: {e.Message}");
        }
        
        return AnimatorControllerParameterType.Float;
    }

    private void DrawMixSettingsSection()
    {
        if (transitionOffset == null || transitionDuration == null || blendCurve == null) return;
        
        EditorGUILayout.LabelField("Mix Settings", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical(GUI.skin.box);
        {
            EditorGUILayout.PropertyField(transitionOffset, new GUIContent("Transition Offset"));
            EditorGUILayout.PropertyField(transitionDuration, new GUIContent("Transition Duration"));
            EditorGUILayout.PropertyField(blendCurve, new GUIContent("Blend Curve"));
        }
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space();
    }

    private void DrawTransitionControlSection()
    {
        if (canBeInterrupted == null) return;
        
        EditorGUILayout.LabelField("Transition Control", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical(GUI.skin.box);
        {
            EditorGUILayout.PropertyField(canBeInterrupted, new GUIContent("Can Be Interrupted"));
        }
        EditorGUILayout.EndVertical();
    }
}