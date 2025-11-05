using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CustomAnimatorEditor : EditorWindow
{
    private CustomAnimatorController _targetController;
    private Vector2 _parameterScroll;
    private Vector2 _layerScroll;
    private Vector2 _stateMachineScroll;
    private CustomAnimationLayer _selectedLayer;
    private Dictionary<CustomAnimationState, Rect> _stateRects = new();
    private CustomAnimationState _selectedState;
    private CustomAnimationTransition _selectedTransition;
    private bool _isDraggingState;
    private CustomAnimationState _draggingState;
    private Rect _tempDragRect;

    [MenuItem("Window/Custom Animator Editor")]
    public static void OpenEditor()
    {
        GetWindow<CustomAnimatorEditor>("Custom Animator Editor");
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        OnSelectionChanged();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        if (Selection.activeObject is CustomAnimatorController controller)
        {
            _targetController = controller;
            _selectedState = null;
            _selectedTransition = null;
            if (_targetController.layers.Count > 0)
                _selectedLayer = _targetController.layers[0];
            InitStateRects();
            Repaint();
        }
        else
        {
            _targetController = null;
            _selectedLayer = null;
            _selectedState = null;
            _selectedTransition = null;
            _stateRects.Clear();
        }
    }

    private void InitStateRects()
    {
        _stateRects.Clear();
        if (_selectedLayer == null) return;

        float x = 50f;
        float y = 50f;
        foreach (var state in _selectedLayer.states)
        {
            _stateRects[state] = new Rect(x, y, 150, 80);
            x += 200f;
            if (x > 800f)
            {
                x = 50f;
                y += 120f;
            }
        }
    }

    private void OnGUI()
    {
        if (_targetController == null)
        {
            GUILayout.Label("请在Project窗口选择一个 Custom Animator Controller", EditorStyles.boldLabel);
            return;
        }

        // 顶部：选择Avatar
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("关联Avatar：", GUILayout.Width(80));
        _targetController.avatar = (Avatar)EditorGUILayout.ObjectField(_targetController.avatar, typeof(Avatar), false);
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);

        // 左侧：参数面板
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.Width(300));
        GUILayout.Label("动画参数", EditorStyles.boldLabel);
        _parameterScroll = EditorGUILayout.BeginScrollView(_parameterScroll);

        for (int i = 0; i < _targetController.parameters.Count; i++)
        {
            var param = _targetController.parameters[i];
            EditorGUILayout.BeginHorizontal();
            param.name = EditorGUILayout.TextField(param.name, GUILayout.Width(120));
            param.type = (AnimatorControllerParameterType)EditorGUILayout.EnumPopup(param.type, GUILayout.Width(100));
            
            switch (param.type)
            {
                case AnimatorControllerParameterType.Float:
                    param.floatValue = EditorGUILayout.FloatField(param.floatValue);
                    break;
                case AnimatorControllerParameterType.Int:
                    param.floatValue = EditorGUILayout.IntField((int)param.floatValue);
                    break;
                case AnimatorControllerParameterType.Bool:
                    param.boolValue = EditorGUILayout.Toggle(param.boolValue);
                    break;
                case AnimatorControllerParameterType.Trigger:
                    if (GUILayout.Button("触发", GUILayout.Width(60)))
                        param.triggerValue = true;
                    break;
            }
            
            if (GUILayout.Button("-", GUILayout.Width(20)))
            {
                _targetController.parameters.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("添加参数"))
        {
            _targetController.parameters.Add(new CustomAnimatorParameter
            {
                name = "NewParam",
                type = AnimatorControllerParameterType.Float
            });
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // 右侧：图层+状态机视图
        EditorGUILayout.BeginVertical();
        // 图层选择面板
        EditorGUILayout.BeginVertical(GUILayout.Height(150));
        GUILayout.Label("动画图层", EditorStyles.boldLabel);
        _layerScroll = EditorGUILayout.BeginScrollView(_layerScroll);

        for (int i = 0; i < _targetController.layers.Count; i++)
        {
            var layer = _targetController.layers[i];
            EditorGUILayout.BeginHorizontal();
            if (layer == _selectedLayer)
                GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);

            layer.layerName = EditorGUILayout.TextField(layer.layerName, GUILayout.Width(120));
            layer.layerWeight = EditorGUILayout.Slider("权重", layer.layerWeight, 0f, 1f, GUILayout.Width(150));
            layer.blendingMode = (CustomBlendingMode)EditorGUILayout.EnumPopup(layer.blendingMode, GUILayout.Width(120));
            layer.mask = (AvatarMask)EditorGUILayout.ObjectField(layer.mask, typeof(AvatarMask), false, GUILayout.Width(100));

            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("设置入口", GUILayout.Width(80)) && layer.states.Count > 0)
                layer.entryStateName = layer.states[0].stateName;

            if (GUILayout.Button("-", GUILayout.Width(20)))
            {
                _targetController.layers.RemoveAt(i);
                if (layer == _selectedLayer)
                    _selectedLayer = _targetController.layers.Count > 0 ? _targetController.layers[0] : null;
                i--;
                InitStateRects();
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("添加图层"))
        {
            var newLayer = new CustomAnimationLayer { layerName = "NewLayer" };
            _targetController.layers.Add(newLayer);
            _selectedLayer = newLayer;
            InitStateRects();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // 状态机视图
        if (_selectedLayer != null)
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label($"[{_selectedLayer.layerName}] 状态机视图", EditorStyles.boldLabel);
            
            GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f);
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            GUI.backgroundColor = Color.white;

            _stateMachineScroll = EditorGUILayout.BeginScrollView(_stateMachineScroll);
            HandleStateDragAndDrop();
            DrawStateNodes();
            DrawTransitions();
            
            EditorGUILayout.Space(20);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("添加动画状态", GUILayout.Width(120)))
            {
                _selectedLayer.states.Add(new CustomAnimationState
                {
                    stateName = "NewState",
                    clip = null
                });
                InitStateRects();
            }
            
            if (GUILayout.Button("创建过渡 (从选中状态)", GUILayout.Width(160)) && _selectedState != null && _selectedLayer.states.Count >= 2)
            {
                var targetState = _selectedLayer.states[0] == _selectedState ? _selectedLayer.states[1] : _selectedLayer.states[0];
                _selectedLayer.transitions.Add(new CustomAnimationTransition
                {
                    fromState = _selectedState,
                    toState = targetState,
                    conditionParamName = _targetController.parameters.Count > 0 ? _targetController.parameters[0].name : ""
                });
            }
            EditorGUILayout.EndHorizontal();

            // 选中状态属性编辑
            if (_selectedState != null)
            {
                EditorGUILayout.Space(10);
                GUILayout.Label("状态属性", EditorStyles.boldLabel);
                _selectedState.stateName = EditorGUILayout.TextField("状态名称", _selectedState.stateName);
                _selectedState.clip = (AnimationClip)EditorGUILayout.ObjectField("动画片段", _selectedState.clip, typeof(AnimationClip), false);
                _selectedState.speed = EditorGUILayout.FloatField("播放速度", _selectedState.speed);
                _selectedState.loop = EditorGUILayout.Toggle("循环播放", _selectedState.loop);
                _selectedState.exitTime = EditorGUILayout.Slider("退出时间", _selectedState.exitTime, 0.1f, 1f);
                
                if (GUILayout.Button("删除选中状态"))
                {
                    // 移除状态及关联过渡
                    var stateToRemove = _selectedState;
                    _selectedLayer.states.Remove(stateToRemove);
                    _selectedLayer.transitions.RemoveAll(t => t.fromState == stateToRemove || t.toState == stateToRemove);
                    _selectedState = null;
                    InitStateRects();
                }
            }

            // 选中过渡属性编辑
            if (_selectedTransition != null)
            {
                EditorGUILayout.Space(10);
                GUILayout.Label("过渡属性", EditorStyles.boldLabel);
                
            // 替换原有的fromState和toState的ObjectField部分
            // 从状态选择
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("从状态:", GUILayout.Width(60));
            var fromStates = _selectedLayer.states.Select(s => s.stateName).ToList();
            int fromIndex = fromStates.IndexOf(_selectedTransition.fromState?.stateName ?? "");
            fromIndex = EditorGUILayout.Popup(fromIndex, fromStates.ToArray(), GUILayout.Width(150));
            if (fromIndex >= 0 && fromIndex < fromStates.Count)
                _selectedTransition.fromState = _selectedLayer.states[fromIndex];
            EditorGUILayout.EndHorizontal();

            // 到状态选择
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("到状态:", GUILayout.Width(60));
            var toStates = _selectedLayer.states.Select(s => s.stateName).ToList();
            int toIndex = toStates.IndexOf(_selectedTransition.toState?.stateName ?? "");
            toIndex = EditorGUILayout.Popup(toIndex, toStates.ToArray(), GUILayout.Width(150));
            if (toIndex >= 0 && toIndex < toStates.Count)
                _selectedTransition.toState = _selectedLayer.states[toIndex];
            EditorGUILayout.EndHorizontal();

                // 条件参数选择
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("条件参数:", GUILayout.Width(60));
                var paramNames = _targetController.parameters.Select(p => p.name).ToList();
                int selectedIndex = paramNames.IndexOf(_selectedTransition.conditionParamName);
                selectedIndex = EditorGUILayout.Popup(selectedIndex, paramNames.ToArray());
                if (selectedIndex >= 0 && selectedIndex < paramNames.Count)
                    _selectedTransition.conditionParamName = paramNames[selectedIndex];
                EditorGUILayout.EndHorizontal();

                _selectedTransition.transitionDuration = EditorGUILayout.FloatField("过渡时长", _selectedTransition.transitionDuration);
                
                if (GUILayout.Button("删除选中过渡"))
                {
                    _selectedLayer.transitions.Remove(_selectedTransition);
                    _selectedTransition = null;
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        if (GUI.changed)
            EditorUtility.SetDirty(_targetController);
    }

    private void HandleStateDragAndDrop()
    {
        Event currentEvent = Event.current;
        Rect viewRect = GUILayoutUtility.GetLastRect();
        viewRect.position += _stateMachineScroll;

        if (_isDraggingState)
        {
            _tempDragRect.position = currentEvent.mousePosition - _dragOffset + _stateMachineScroll;
            if (currentEvent.type == EventType.MouseUp)
            {
                _stateRects[_draggingState] = _tempDragRect;
                _isDraggingState = false;
                _draggingState = null;
                currentEvent.Use();
            }
        }
        else
        {
            foreach (var state in _selectedLayer.states)
            {
                Rect stateRect = _stateRects[state];
                if (stateRect.Contains(currentEvent.mousePosition - _stateMachineScroll))
                {
                    if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
                    {
                        _selectedState = state;
                        _selectedTransition = null;
                        _isDraggingState = true;
                        _draggingState = state;
                        _tempDragRect = stateRect;
                        _dragOffset = currentEvent.mousePosition - stateRect.position - _stateMachineScroll;
                        currentEvent.Use();
                    }
                }
            }
        }
    }

    private Vector2 _dragOffset;

    private void DrawStateNodes()
    {
        foreach (var state in _selectedLayer.states)
        {
            Rect rect = _isDraggingState && _draggingState == state ? _tempDragRect : _stateRects[state];
            
            // 选中状态绘制红色边框
            if (state == _selectedState)
            {
                var originalColor = GUI.color;
                GUI.color = Color.red;
                GUI.Box(rect, "");
                GUI.color = originalColor;
            }
            else
            {
                GUI.Box(rect, "");
            }

            GUI.Label(new Rect(rect.x + 10, rect.y + 10, rect.width - 20, 20), state.stateName, EditorStyles.boldLabel);

            state.clip = (AnimationClip)EditorGUI.ObjectField(
                new Rect(rect.x + 10, rect.y + 35, rect.width - 20, 20), 
                state.clip, 
                typeof(AnimationClip), 
                false
            );
            GUI.Label(new Rect(rect.x + 10, rect.y + 60, 60, 20), "ExitTime:");
            state.exitTime = EditorGUI.Slider(
                new Rect(rect.x + 70, rect.y + 60, rect.width - 80, 20), 
                state.exitTime, 
                0.1f, 
                1f
            );
        }
    }

    private void DrawTransitions()
    {
        Event currentEvent = Event.current;
        foreach (var transition in _selectedLayer.transitions)
        {
            if (transition.fromState == null || transition.toState == null) continue;
            if (!_stateRects.ContainsKey(transition.fromState) || !_stateRects.ContainsKey(transition.toState)) continue;

            var fromRect = _stateRects[transition.fromState];
            var toRect = _stateRects[transition.toState];
            Vector2 startPos = new Vector2(fromRect.x + fromRect.width, fromRect.y + fromRect.height / 2) + _stateMachineScroll;
            Vector2 endPos = new Vector2(toRect.x, toRect.y + toRect.height / 2) + _stateMachineScroll;

            // 检测过渡点击
            if (IsPointOnLine(currentEvent.mousePosition, startPos, endPos, 5f) && 
                currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                _selectedTransition = transition;
                _selectedState = null;
                currentEvent.Use();
            }

            // 绘制连线
            Handles.color = transition == _selectedTransition ? Color.blue : Color.black;
            Handles.DrawLine(startPos, endPos);
            DrawArrow(endPos, startPos - endPos);
            
            // 显示过渡条件
            Vector2 midPos = (startPos + endPos) / 2;
            var param = _targetController.GetParameter(transition.conditionParamName);
            if (param != null)
                GUI.Label(new Rect(midPos.x - 50, midPos.y - 10, 100, 20), 
                    $"条件: {param.name}", EditorStyles.miniLabel);
        }
    }

    private bool IsPointOnLine(Vector2 point, Vector2 lineStart, Vector2 lineEnd, float tolerance)
    {
        // 计算点到直线的垂直距离（使用Vector3.Cross是为了利用Z轴计算二维叉积）
        float distance = Vector3.Cross(lineEnd - lineStart, point - lineStart).magnitude;
        
        // 计算方向向量的点积（判断是否在同方向）
        float dot = Vector2.Dot((point - lineStart).normalized, (lineEnd - lineStart).normalized);
        
        // 修正：将lengthCheck改为bool类型（比较运算结果是bool）
        bool lengthCheck = (point - lineStart).magnitude <= (lineEnd - lineStart).magnitude;
        
        // 三个条件同时满足：距离在容差内、方向一致、在线段长度范围内
        return distance <= tolerance && dot >= 0 && lengthCheck;
    }

    private void DrawArrow(Vector2 endPos, Vector2 direction)
    {
        const float arrowAngle = 20f;
        const float arrowLength = 10f;

        // 将Vector2转换为Vector3进行旋转计算，明确使用浮点运算
        Vector3 dir3D = new Vector3(direction.x, direction.y, 0f).normalized;
        Vector3 right3D = Quaternion.Euler(0, 0, arrowAngle) * dir3D * arrowLength;
        Vector3 left3D = Quaternion.Euler(0, 0, -arrowAngle) * dir3D * arrowLength;

        // 转回Vector2绘制
        Vector2 right = new Vector2(right3D.x, right3D.y);
        Vector2 left = new Vector2(left3D.x, left3D.y);

        Handles.DrawLine(endPos, endPos + right);
        Handles.DrawLine(endPos, endPos + left);
    }
}