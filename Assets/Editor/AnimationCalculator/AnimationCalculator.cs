using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using Object = UnityEngine.Object;

public class AnimationCalculator : EditorWindow
{
    private List<AnimationEntry> animationEntries = new List<AnimationEntry>();
    private List<ResultEntry> resultEntries = new List<ResultEntry>();
    private Vector2 scrollPos;
    private string expression = "A+B";
    private string lastUsedPath = "Assets";
    private float resultListHeight = EditorGUIUtility.singleLineHeight;
    private const float maxResultListHeight = 200f;
    private Texture2D headerTexture;
    private bool textureLoaded = false;
    private Rect animationListRect; // 动画列表区域，用于限制拖拽范围

    // 菜单栏和右键菜单入口
    [MenuItem("Tools/Animation/Animation Calculator")]
    [MenuItem("Assets/Animation/Animation Calculator", false, 30)]
    private static void ShowWindow()
    {
        ShowWindowWithSelection();
    }

    // 资产右键菜单
    [MenuItem("Assets/Animation Calculator")]
    private static void ShowWindowFromAssets()
    {
        ShowWindowWithSelection();
    }

    // 动画剪辑右键菜单
    [MenuItem("CONTEXT/AnimationClip/Animation Calculator")]
    private static void ShowWindowFromAnimationClip()
    {
        ShowWindowWithSelection();
    }

    // 显示窗口并处理选中对象
    private static void ShowWindowWithSelection()
    {
        var window = GetWindow<AnimationCalculator>("Animation Calculator");
        window.minSize = new Vector2(500, 400);
        window.Show();
        window.AddSelectedObjects();
    }

    // 添加选中的对象到列表
    private void AddSelectedObjects()
    {
        foreach (var obj in Selection.objects)
        {
            ProcessDroppedItem(obj);
        }
    }

    private void OnGUI()
    {
        HandleDragAndDrop();
        
        EditorGUILayout.Space();
        if (!textureLoaded)
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            string directory = Path.GetDirectoryName(scriptPath);
            string imagePath = Path.Combine(directory, "AnimationCalculator.png");
            
            headerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
            textureLoaded = true;
        }
        
        if (headerTexture != null)
        {
            float aspectRatio = (float)headerTexture.height / headerTexture.width;
            float imageHeight = position.width * aspectRatio;
            
            Rect imageRect = GUILayoutUtility.GetRect(position.width, imageHeight);
            GUI.DrawTexture(imageRect, headerTexture, ScaleMode.ScaleToFit);
        }
        else
        {
            GUILayout.Label("Animation Calculator", EditorStyles.boldLabel);
        }
        EditorGUILayout.Space();
        
        // 绘制动画列表并获取其区域（修复：不使用BeginArea/EndArea，改用GetLastRect获取区域）
        EditorGUILayout.BeginVertical(GUILayout.Height(150)); // 限制列表高度
        DrawAnimationList();
        EditorGUILayout.EndVertical();
        animationListRect = GUILayoutUtility.GetLastRect(); // 获取刚绘制的列表区域

        DrawExpressionEditor();
        DrawCalculateButton();
        DrawResultList();
    }

    #region 拖拽处理
    private void HandleDragAndDrop()
    {
        // 仅在动画列表区域内处理拖拽
        // if (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform)
        // {
        //     // 转换鼠标位置到窗口局部坐标（Y轴反转，因为GUI原点在左上角）
        //     Vector2 mousePos = Event.current.mousePosition;
        //     mousePos.y = position.height - mousePos.y;

        //     // 检查是否在动画列表区域内
        //     if (!animationListRect.Contains(mousePos))
        //     {
        //         return;
        //     }
        // }

        Rect dropArea = new Rect(0, 0, position.width, position.height);
        Event evt = Event.current;
        switch (evt.type)
        {
            case EventType.DragUpdated:
                if (dropArea.Contains(evt.mousePosition))
                {
                bool containsValidFiles = DragAndDrop.objectReferences.Any(IsValidDragObject);
                DragAndDrop.visualMode = containsValidFiles ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                evt.Use();
                }
                break;

            case EventType.DragPerform:
                if (dropArea.Contains(evt.mousePosition))
                {
                DragAndDrop.AcceptDrag();
                ProcessDroppedItems(DragAndDrop.objectReferences);
                evt.Use();
                }
                break;
        }
    }

    private bool IsValidDragObject(Object obj)
    {
        string path = AssetDatabase.GetAssetPath(obj);
        return (obj is AnimationClip) || 
               (obj is GameObject && Path.GetExtension(path).ToLower() == ".fbx") ||
               (obj is DefaultAsset && Directory.Exists(path));
    }

    private void ProcessDroppedItems(Object[] droppedObjects)
    {
        foreach (var obj in droppedObjects)
        {
            ProcessDroppedItem(obj);
        }
    }

    private void ProcessDroppedItem(Object obj)
    {
        string path = AssetDatabase.GetAssetPath(obj);
        
        if (obj is AnimationClip)
        {
            AddAnimationEntry(obj as AnimationClip);
        }
        else if (obj is GameObject && Path.GetExtension(path).ToLower() == ".fbx")
        {
            ProcessFBXFile(obj as GameObject);
        }
        else if (obj is DefaultAsset && Directory.Exists(path))
        {
            ProcessFolder(path);
        }
    }
    #endregion

    #region 文件处理
    private void ProcessFolder(string folderPath)
    {
        string dataPath = Application.dataPath;
        string folderFullPath = Path.GetFullPath(folderPath);
        
        var animFiles = Directory.GetFiles(folderFullPath, "*.anim", SearchOption.AllDirectories)
            .Where(p => !p.EndsWith(".meta"));
        
        foreach (var file in animFiles)
        {
            string relativePath = "Assets" + file.Substring(dataPath.Length);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(relativePath);
            if (clip != null) AddAnimationEntry(clip);
        }
        
        var fbxFiles = Directory.GetFiles(folderFullPath, "*.fbx", SearchOption.AllDirectories)
            .Where(p => !p.EndsWith(".meta"));
        
        foreach (var file in fbxFiles)
        {
            string relativePath = "Assets" + file.Substring(dataPath.Length);
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
            if (fbx != null) ProcessFBXFile(fbx);
        }
    }

    private void ProcessFBXFile(GameObject fbx)
    {
        string path = AssetDatabase.GetAssetPath(fbx);
        var clips = AssetDatabase.LoadAllAssetsAtPath(path)
            .Where(x => x is AnimationClip)
            .Cast<AnimationClip>()
            .Where(x => !x.name.StartsWith("preview_") && 
                       !x.name.StartsWith("__preview__") && 
                       !x.name.EndsWith("_preview"))
            .ToList();
        
        foreach (var clip in clips)
        {
            AddAnimationEntry(clip, fbx.name + "_" + clip.name);
        }
    }

    private void AddAnimationEntry(AnimationClip clip, string defaultName = null)
    {
        if (clip == null) return;
        
        if (!animationEntries.Any(x => x.clip == clip))
        {
            animationEntries.Add(new AnimationEntry
            {
                clip = clip,
                variableName = defaultName ?? ("P_" + (animationEntries.Count + 1))
            });
        }
    }
    #endregion

    #region UI绘制
    private void DrawAnimationList()
    {
        EditorGUILayout.LabelField("Animation Clips:", EditorStyles.boldLabel);
        
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        {
            for (int i = 0; i < animationEntries.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    animationEntries[i].clip = EditorGUILayout.ObjectField(
                        animationEntries[i].clip, typeof(AnimationClip), false) as AnimationClip;
                    
                    animationEntries[i].variableName = EditorGUILayout.TextField(
                        animationEntries[i].variableName, GUILayout.Width(100));
                    
                    if (GUILayout.Button("×", GUILayout.Width(20)))
                    {
                        animationEntries.RemoveAt(i);
                        i--;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Add Clip"))
                {
                    var path = EditorUtility.OpenFilePanelWithFilters("Select Animation Clip", 
                        lastUsedPath, new[] { "Animation Clip", "anim" });
                    
                    if (!string.IsNullOrEmpty(path))
                    {
                        lastUsedPath = Path.GetDirectoryName(path);
                        string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(relativePath);
                        if (clip != null) AddAnimationEntry(clip);
                    }
                }
                
                if (GUILayout.Button("Add FBX"))
                {
                    var path = EditorUtility.OpenFilePanelWithFilters("Select FBX File", 
                        lastUsedPath, new[] { "FBX Files", "fbx" });
                    
                    if (!string.IsNullOrEmpty(path))
                    {
                        lastUsedPath = Path.GetDirectoryName(path);
                        string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
                        if (fbx != null) ProcessFBXFile(fbx);
                    }
                }
                
                if (GUILayout.Button("Clear All"))
                {
                    animationEntries.Clear();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawExpressionEditor()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Expression:", EditorStyles.boldLabel);
        
        expression = EditorGUILayout.TextField(expression, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        
        EditorGUILayout.HelpBox("支持数学表达式和变量:\n" +
                               "运算符: + - * / ( )\n" +
                               "规则: 动画之间只能使用 + 和 -\n" +
                               "动画与数字之间可以使用所有运算符\n" +
                               "示例: (Walk+Run)*0.5", MessageType.Info);
    }

    private void DrawCalculateButton()
    {
        if (GUILayout.Button("Calculate", GUILayout.Height(30)))
        {
            try
            {
                var resultClip = CalculateAnimation();
                if (resultClip != null)
                {
                    resultEntries.Add(new ResultEntry
                    {
                        clip = resultClip,
                        name = "Result_" + resultEntries.Count,
                        fbxReference = null
                    });
                    
                    // 更新结果列表高度
                    resultListHeight = Mathf.Min(
                        EditorGUIUtility.singleLineHeight * (resultEntries.Count + 1), 
                        maxResultListHeight);
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("错误", e.Message, "确定");
            }
        }
    }

    private void DrawResultList()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Results:", EditorStyles.boldLabel);
        
        // 动态调整高度的结果列表
        if (resultEntries.Count > 0)
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(resultListHeight));
            {
                for (int i = 0; i < resultEntries.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        resultEntries[i].name = EditorGUILayout.TextField(resultEntries[i].name);
                        
                        EditorGUI.BeginChangeCheck();
                        var newFbx = EditorGUILayout.ObjectField(
                            resultEntries[i].fbxReference, 
                            typeof(GameObject), 
                            false) as GameObject;
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (newFbx != null)
                            {
                                string path = AssetDatabase.GetAssetPath(newFbx);
                                if (Path.GetExtension(path).ToLower() != ".fbx")
                                {
                                    EditorUtility.DisplayDialog("错误", "只允许FBX文件", "确定");
                                    continue;
                                }
                            }
                            resultEntries[i].fbxReference = newFbx;
                        }
                        
                        if (GUILayout.Button("Save", GUILayout.Width(60)))
                        {
                            SaveResultClip(i);
                        }
                        
                        if (GUILayout.Button("×", GUILayout.Width(20)))
                        {
                            resultEntries.RemoveAt(i);
                            i--;
                            resultListHeight = Mathf.Min(
                                EditorGUIUtility.singleLineHeight * (resultEntries.Count + 1), 
                                maxResultListHeight);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.LabelField("暂无结果", EditorStyles.centeredGreyMiniLabel);
        }
    }
    #endregion

    #region 动画计算
    private AnimationClip CalculateAnimation()
    {
        if (string.IsNullOrEmpty(expression))
        {
            throw new Exception("表达式不能为空");
        }
        
        // 检查是否是单个变量
        var singleVar = animationEntries.FirstOrDefault(e => 
            e.variableName == expression.Trim());
        
        if (singleVar != null)
        {
            // 直接返回该变量的Clip副本
            var newClip = new AnimationClip();
            EditorUtility.CopySerialized(singleVar.clip, newClip);
            return newClip;
        }
        
        var parsedExpression = ParseExpression(expression);
        if (!parsedExpression.IsValid)
        {
            throw new Exception(parsedExpression.ErrorMessage);
        }
        
        // 对齐所有动画的属性和关键帧
        var alignedClips = AlignAllAnimations();
        var resultClip = ApplyExpressionToAnimation(alignedClips, parsedExpression);
        
        return resultClip;
    }

    /// <summary>
    /// 对齐所有动画的骨骼和关键帧
    /// 确保所有动画拥有相同的骨骼集合，缺失的骨骼用默认值填充
    /// </summary>
    private Dictionary<string, AnimationClip> AlignAllAnimations()
    {
        // 1. 收集所有动画中存在的骨骼绑定
        var allBindings = new HashSet<EditorCurveBinding>();
        foreach (var entry in animationEntries.Where(e => e.clip != null))
        {
            var bindings = AnimationUtility.GetCurveBindings(entry.clip)
                .Where(IsValidBinding)
                .ToList();
                
            foreach (var binding in bindings)
            {
                allBindings.Add(binding);
            }
        }

        // 2. 为每个动画创建对齐后的版本
        var alignedClips = new Dictionary<string, AnimationClip>();
        foreach (var entry in animationEntries.Where(e => e.clip != null))
        {
            var originalClip = entry.clip;
            var alignedClip = new AnimationClip();
            
            // 为每个骨骼绑定设置曲线，缺失的骨骼用默认值
            foreach (var binding in allBindings)
            {
                // 检查原动画是否有这个绑定
                var originalCurve = AnimationUtility.GetEditorCurve(originalClip, binding);
                if (originalCurve != null)
                {
                    // 原动画有这个骨骼，直接使用其曲线
                    alignedClip.SetCurve(binding.path, binding.type, binding.propertyName, originalCurve);
                }
                else
                {
                    // 原动画没有这个骨骼，创建默认曲线
                    float defaultValue = GetDefaultValueForProperty(binding.propertyName);
                    var defaultCurve = AnimationCurve.Constant(0, 0, defaultValue);
                    alignedClip.SetCurve(binding.path, binding.type, binding.propertyName, defaultCurve);
                }
            }
            
            // 3. 对齐关键帧时间（使用最长的动画时长）
            float maxLength = animationEntries.Max(e => e.clip != null ? e.clip.length : 0);
            AdjustAnimationLength(alignedClip, maxLength);
            
            alignedClips[entry.variableName] = alignedClip;
        }
        
        return alignedClips;
    }

    /// <summary>
    /// 调整动画长度到指定时长
    /// 通过延长最后一个关键帧来实现
    /// </summary>
    private void AdjustAnimationLength(AnimationClip clip, float targetLength)
    {
        if (clip.length >= targetLength) return;
        
        var bindings = AnimationUtility.GetCurveBindings(clip);
        foreach (var binding in bindings)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve.keys.Length == 0) continue;
            
            // 延长最后一个关键帧的时间
            Keyframe lastKey = curve.keys[curve.keys.Length - 1];
            if (lastKey.time < targetLength)
            {
                lastKey.time = targetLength;
                curve.keys[curve.keys.Length - 1] = lastKey;
                clip.SetCurve(binding.path, binding.type, binding.propertyName, curve);
            }
        }
    }

    private ParsedExpression ParseExpression(string input)
    {
        var result = new ParsedExpression();
        input = input.Trim();
        
        try
        {
            int pos = 0;
            while (pos < input.Length)
            {
                char c = input[pos];
                
                // 跳过空格
                if (char.IsWhiteSpace(c))
                {
                    pos++;
                    continue;
                }
                
                // 处理括号
                if (c == '(' || c == ')')
                {
                    result.Tokens.Add(new ExpressionToken {
                        Type = c == '(' ? TokenType.OpenParen : TokenType.CloseParen,
                        Value = c.ToString()
                    });
                    pos++;
                    continue;
                }
                
                // 处理运算符
                if (IsOperator(c))
                {
                    result.Tokens.Add(new ExpressionToken {
                        Type = TokenType.Operator,
                        Value = c.ToString()
                    });
                    pos++;
                    continue;
                }
                
                // 处理数字
                if (char.IsDigit(c) || c == '.')
                {
                    string numStr = ParseNumber(input, ref pos);
                    result.Tokens.Add(new ExpressionToken {
                        Type = TokenType.Number,
                        Value = numStr
                    });
                    continue;
                }
                
                // 处理变量名
                if (char.IsLetter(c) || c == '_')
                {
                    string varName = ParseVariableName(input, ref pos);
                    
                    // 检查变量是否定义
                    var clip = animationEntries.FirstOrDefault(x => x.variableName == varName)?.clip;
                    if (clip == null)
                    {
                        throw new Exception($"未定义的变量: {varName}");
                    }
                    
                    result.Tokens.Add(new ExpressionToken {
                        Type = TokenType.Variable,
                        Value = varName,
                        Clip = clip
                    });
                    continue;
                }
                
                throw new Exception($"无效字符: '{c}'");
            }
            
            // 验证括号匹配
            ValidateParentheses(result.Tokens);
            
            // 转换为逆波兰表达式并验证运算符规则
            result.Tokens = ConvertToRPN(result.Tokens);
            ValidateOperatorRules(result.Tokens);
            
            result.IsValid = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }
        
        return result;
    }

    /// <summary>
    /// 验证运算符使用规则
    /// 动画之间只能使用+和-，动画与数字之间可以使用所有运算符
    /// </summary>
    private void ValidateOperatorRules(List<ExpressionToken> rpnTokens)
    {
        var stack = new Stack<TokenType>();
        
        foreach (var token in rpnTokens)
        {
            if (token.Type == TokenType.Number || token.Type == TokenType.Variable)
            {
                stack.Push(token.Type);
            }
            else if (token.Type == TokenType.Operator)
            {
                if (stack.Count < 2)
                {
                    throw new Exception($"运算符 '{token.Value}' 缺少操作数");
                }
                
                var bType = stack.Pop();
                var aType = stack.Pop();
                
                // 两个操作数都是变量（动画）
                if (aType == TokenType.Variable && bType == TokenType.Variable)
                {
                    if (token.Value == "*" || token.Value == "/")
                    {
                        throw new Exception($"不允许对两个动画使用 '{token.Value}' 运算符");
                    }
                }
                
                // 结果类型：只要有一个是变量，结果就视为变量
                stack.Push(aType == TokenType.Variable || bType == TokenType.Variable 
                    ? TokenType.Variable 
                    : TokenType.Number);
            }
        }
    }

    private string ParseNumber(string input, ref int pos)
    {
        int start = pos;
        bool hasDot = false;
        
        while (pos < input.Length)
        {
            char c = input[pos];
            
            if (char.IsDigit(c))
            {
                pos++;
            }
            else if (c == '.' && !hasDot)
            {
                hasDot = true;
                pos++;
            }
            else
            {
                break;
            }
        }
        
        return input.Substring(start, pos - start);
    }

    private string ParseVariableName(string input, ref int pos)
    {
        int start = pos;
        
        while (pos < input.Length)
        {
            char c = input[pos];
            
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                pos++;
            }
            else
            {
                break;
            }
        }
        
        return input.Substring(start, pos - start);
    }

    private void ValidateParentheses(List<ExpressionToken> tokens)
    {
        int balance = 0;
        foreach (var token in tokens)
        {
            if (token.Type == TokenType.OpenParen) balance++;
            else if (token.Type == TokenType.CloseParen) balance--;
            
            if (balance < 0)
            {
                throw new Exception("不匹配的右括号");
            }
        }
        
        if (balance != 0)
        {
            throw new Exception("不匹配的左括号");
        }
    }

    private List<ExpressionToken> ConvertToRPN(List<ExpressionToken> tokens)
    {
        var output = new List<ExpressionToken>();
        var stack = new Stack<ExpressionToken>();
        
        foreach (var token in tokens)
        {
            switch (token.Type)
            {
                case TokenType.Number:
                case TokenType.Variable:
                    output.Add(token);
                    break;
                    
                case TokenType.OpenParen:
                    stack.Push(token);
                    break;
                    
                case TokenType.CloseParen:
                    while (stack.Count > 0 && stack.Peek().Type != TokenType.OpenParen)
                    {
                        output.Add(stack.Pop());
                    }
                    
                    if (stack.Count == 0)
                    {
                        throw new Exception("括号不匹配");
                    }
                    
                    stack.Pop(); // 弹出左括号
                    break;
                    
                case TokenType.Operator:
                    while (stack.Count > 0 && stack.Peek().Type == TokenType.Operator && 
                           GetOperatorPriority(stack.Peek().Value) >= GetOperatorPriority(token.Value))
                    {
                        output.Add(stack.Pop());
                    }
                    stack.Push(token);
                    break;
            }
        }
        
        while (stack.Count > 0)
        {
            if (stack.Peek().Type == TokenType.OpenParen)
            {
                throw new Exception("括号不匹配");
            }
            output.Add(stack.Pop());
        }
        
        return output;
    }

    private bool IsOperator(char c)
    {
        return c == '+' || c == '-' || c == '*' || c == '/';
    }

    private int GetOperatorPriority(string op)
    {
        switch (op)
        {
            case "*":
            case "/":
                return 2;
            case "+":
            case "-":
                return 1;
            default:
                return 0;
        }
    }

    private bool IsValidBinding(EditorCurveBinding binding)
    {
        // 只处理Transform的标准属性
        if (binding.type != typeof(Transform)) return false;
        
        switch (binding.propertyName)
        {
            case "m_LocalPosition.x":
            case "m_LocalPosition.y":
            case "m_LocalPosition.z":
            case "localEulerAngles.x":
            case "localEulerAngles.y":
            case "localEulerAngles.z":
            case "m_LocalScale.x":
            case "m_LocalScale.y":
            case "m_LocalScale.z":
                return true;
            default:
                return false;
        }
    }

    private AnimationClip ApplyExpressionToAnimation(Dictionary<string, AnimationClip> alignedClips, ParsedExpression parsedExpression)
    {
        if (alignedClips.Count == 0)
        {
            throw new Exception("没有有效的动画片段可用于计算");
        }
        
        // 获取所有骨骼绑定（所有动画都已对齐，使用第一个动画的绑定即可）
        var firstClip = alignedClips.Values.First();
        var bindings = AnimationUtility.GetCurveBindings(firstClip)
            .Where(IsValidBinding)
            .ToList();
        
        // 获取关键帧数量（取最长的动画）
        int frameCount = 0;
        foreach (var clip in alignedClips.Values)
        {
            var curve = AnimationUtility.GetCurveBindings(clip).Select(b => AnimationUtility.GetEditorCurve(clip, b)).FirstOrDefault();
            if (curve != null && curve.keys.Length > frameCount)
            {
                frameCount = curve.keys.Length;
            }
        }
        
        var resultClip = new AnimationClip();
        
        // 为每个骨骼属性应用表达式计算
        foreach (var binding in bindings)
        {
            var newCurve = new AnimationCurve();
            
            // 为每个关键帧计算值
            for (int i = 0; i < frameCount; i++)
            {
                float time = 0;
                // 获取当前帧时间（使用第一个曲线的时间）
                var firstCurve = AnimationUtility.GetEditorCurve(firstClip, binding);
                if (i < firstCurve.keys.Length)
                {
                    time = firstCurve.keys[i].time;
                }
                
                // 计算当前帧的值
                float resultValue = EvaluateExpressionForFrame(parsedExpression, alignedClips, binding, i);
                newCurve.AddKey(time, resultValue);
            }
            
            resultClip.SetCurve(binding.path, binding.type, binding.propertyName, newCurve);
        }
        
        return resultClip;
    }

    private float EvaluateExpressionForFrame(ParsedExpression expression, Dictionary<string, AnimationClip> alignedClips, 
                                             EditorCurveBinding binding, int frameIndex)
    {
        var stack = new Stack<float>();
        
        foreach (var token in expression.Tokens)
        {
            switch (token.Type)
            {
                case TokenType.Number:
                    stack.Push(float.Parse(token.Value));
                    break;
                    
                case TokenType.Variable:
                    // 从对齐后的动画中获取当前帧的值
                    if (alignedClips.TryGetValue(token.Value, out var clip))
                    {
                        var curve = AnimationUtility.GetEditorCurve(clip, binding);
                        if (curve != null && frameIndex < curve.keys.Length)
                        {
                            stack.Push(curve.keys[frameIndex].value);
                        }
                        else
                        {
                            // 没有关键帧时使用默认值
                            stack.Push(GetDefaultValueForProperty(binding.propertyName));
                        }
                    }
                    else
                    {
                        throw new Exception($"变量 '{token.Value}' 未找到对齐的动画");
                    }
                    break;
                    
                case TokenType.Operator:
                    if (stack.Count < 2)
                    {
                        throw new Exception($"运算符 '{token.Value}' 没有足够的操作数");
                    }
                    
                    float b = stack.Pop();
                    float a = stack.Pop();
                    
                    switch (token.Value)
                    {
                        case "+": stack.Push(a + b); break;
                        case "-": stack.Push(a - b); break;
                        case "*": stack.Push(a * b); break;
                        case "/": 
                            if (Mathf.Abs(b) < 0.0001f)
                            {
                                throw new Exception("除以零错误");
                            }
                            stack.Push(a / b); 
                            break;
                    }
                    break;
            }
        }
        
        if (stack.Count != 1)
        {
            throw new Exception("表达式计算结果无效");
        }
        
        return stack.Pop();
    }
    #endregion

    #region 结果保存
    private void SaveResultClip(int index)
    {
        if (index < 0 || index >= resultEntries.Count) return;
        
        var entry = resultEntries[index];
        if (entry.clip == null) return;
        
        // 验证所有曲线属性是否有效
        var bindings = AnimationUtility.GetCurveBindings(entry.clip);
        foreach (var binding in bindings)
        {
            if (!IsValidBinding(binding))
            {
                EditorUtility.DisplayDialog("错误", 
                    $"无法保存动画 - 无效属性: {binding.propertyName}", "确定");
                return;
            }
        }
        
        if (entry.fbxReference != null)
        {
            SaveAsFBX(entry);
        }
        else
        {
            SaveAsAnim(entry);
        }
    }

    private void SaveAsAnim(ResultEntry entry)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "保存动画片段",
            entry.name,
            "anim",
            "选择保存位置");
        
        if (!string.IsNullOrEmpty(path))
        {
            // 创建新的Clip副本
            var newClip = new AnimationClip();
            EditorUtility.CopySerialized(entry.clip, newClip);
            
            AssetDatabase.CreateAsset(newClip, path);
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("成功", 
                $"动画片段已保存至: {path}", "确定");
        }
    }

    private void SaveAsFBX(ResultEntry entry)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "保存带动画的FBX",
            entry.name,
            "fbx",
            "选择保存位置");
        
        if (!string.IsNullOrEmpty(path))
        {
            string sourcePath = AssetDatabase.GetAssetPath(entry.fbxReference);
            
            // 复制FBX文件
            if (AssetDatabase.CopyAsset(sourcePath, path))
            {
                // 移除原始动画Clip
                var originalClips = AssetDatabase.LoadAllAssetsAtPath(path)
                    .Where(x => x is AnimationClip).Cast<AnimationClip>().ToList();
                
                foreach (var clip in originalClips)
                {
                    AssetDatabase.RemoveObjectFromAsset(clip);
                }
                
                // 创建匹配FBX骨架的新Clip
                var matchedClip = MatchClipToFBX(entry.clip, entry.fbxReference);
                matchedClip.name = entry.name;
                
                AssetDatabase.AddObjectToAsset(matchedClip, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                EditorUtility.DisplayDialog("成功", 
                    $"带动画的FBX已保存至: {path}", "确定");
            }
        }
    }

    private AnimationClip MatchClipToFBX(AnimationClip sourceClip, GameObject fbx)
    {
        var newClip = new AnimationClip();
        newClip.legacy = false;
        newClip.wrapMode = WrapMode.Loop;
        
        var fbxBindings = AnimationUtility.GetCurveBindings(fbx.GetComponent<Animation>()?.clip ?? new AnimationClip())
            .Where(IsValidBinding)
            .ToList();
        
        var sourceBindings = AnimationUtility.GetCurveBindings(sourceClip)
            .Where(IsValidBinding)
            .ToList();
        
        foreach (var binding in fbxBindings)
        {
            // 查找源Clip中是否有对应的曲线
            var sourceBinding = sourceBindings.FirstOrDefault(b => 
                b.path == binding.path && 
                b.propertyName == binding.propertyName);
            
            if (sourceBinding != null)
            {
                var curve = AnimationUtility.GetEditorCurve(sourceClip, sourceBinding);
                newClip.SetCurve(binding.path, binding.type, binding.propertyName, curve);
            }
            else
            {
                // 使用默认值
                var defaultValue = GetDefaultValueForProperty(binding.propertyName);
                var defaultCurve = AnimationCurve.Linear(0, defaultValue, 1, defaultValue);
                newClip.SetCurve(binding.path, binding.type, binding.propertyName, defaultCurve);
            }
        }
        
        return newClip;
    }

    private float GetDefaultValueForProperty(string propertyName)
    {
        switch (propertyName)
        {
            case "m_LocalPosition.x":
            case "m_LocalPosition.y":
            case "m_LocalPosition.z":
                return 0f;
                
            case "localEulerAngles.x":
            case "localEulerAngles.y":
            case "localEulerAngles.z":
                return 0f;
                
            case "m_LocalScale.x":
            case "m_LocalScale.y":
            case "m_LocalScale.z":
                return 1f;
                
            default:
                return 0f;
        }
    }
    #endregion

    #region 数据类
    [Serializable]
    private class AnimationEntry
    {
        public AnimationClip clip;
        public string variableName;
    }

    [Serializable]
    private class ResultEntry
    {
        public AnimationClip clip;
        public string name;
        public GameObject fbxReference;
    }

    private class ParsedExpression
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public List<ExpressionToken> Tokens { get; set; } = new List<ExpressionToken>();
    }

    private class ExpressionToken
    {
        public TokenType Type { get; set; }
        public string Value { get; set; }
        public AnimationClip Clip { get; set; }
    }

    private enum TokenType
    {
        Number,
        Variable,
        Operator,
        OpenParen,
        CloseParen
    }
    #endregion
}