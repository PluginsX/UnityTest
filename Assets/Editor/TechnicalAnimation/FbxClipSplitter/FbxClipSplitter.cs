// 引入必要的命名空间
using UnityEditor; // Unity编辑器API
using UnityEngine; // Unity引擎核心API
using System.Collections.Generic; // 集合类
using System.IO; // 文件系统操作
using System.Linq; // LINQ扩展方法

// 定义一个继承自EditorWindow的类，用于创建自定义编辑器窗口
public class FbxClipSplitter : EditorWindow
{
    // 存储FBX文件信息的列表
    private List<FBXEntry> fbxEntries = new List<FBXEntry>();
    // 滚动视图的位置
    private Vector2 scrollPos;
    // 记录上次使用的路径，默认为Assets文件夹
    private string lastUsedPath = "Assets";
    // 用于显示在窗口顶部的纹理
    private Texture2D headerTexture;
    // 标记纹理是否已加载
    private bool textureLoaded = false;

    
    [MenuItem("Tools/Animation/FBX Clip Splitter")]// 在Unity菜单栏中添加菜单项
    [MenuItem("Assets/Animation/FBX Clip Splitter", false, 31)]// 在资源右键菜单中添加菜单项
    private static void ShowWindow()
    {
        // 获取或创建窗口实例
        var window = GetWindow<FbxClipSplitter>("FBX Clip Splitter");
        // 使用当前选中的对象初始化窗口
        window.InitializeWithSelection();
        // 显示窗口
        window.Show();
    }

    // 绘制编辑器窗口的GUI
    private void OnGUI()
    {
        // 优先处理拖拽事件
        HandleDragAndDrop();

        // 添加一些空白间距
        EditorGUILayout.Space();

        // 加载图片（仅在第一次时加载）
        if (!textureLoaded)
        {
            // 获取当前脚本的路径
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            // 获取脚本所在目录
            string directory = Path.GetDirectoryName(scriptPath);
            // 拼接图片路径
            string imagePath = Path.Combine(directory, "FbxClipSplitter.png");
            
            // 从路径加载纹理
            headerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
            // 标记纹理已加载
            textureLoaded = true;
        }
        
        // 显示图片（如果加载成功）
        if (headerTexture != null)
        {
            // 计算缩放比例（保持宽高比）
            float aspectRatio = (float)headerTexture.height / headerTexture.width;
            // 根据窗口宽度计算图片高度
            float imageHeight = position.width * aspectRatio;
            
            // 获取绘制区域
            Rect imageRect = GUILayoutUtility.GetRect(position.width, imageHeight);
            // 绘制纹理
            GUI.DrawTexture(imageRect, headerTexture, ScaleMode.ScaleToFit);
        }
        else
        {
            // 如果图片加载失败，显示备用文字标题
            GUILayout.Label("FBX Clip Splitter - Feifan.jiao", EditorStyles.boldLabel);
        }
        // 添加一些空白间距
        EditorGUILayout.Space();

        // 绘制工具栏
        DrawToolbar();
        // 绘制FBX列表
        DrawFBXList();
    }

    // 处理拖拽事件
    private void HandleDragAndDrop()
    {
        // 定义整个窗口为拖拽区域
        Rect dropArea = new Rect(0, 0, position.width, position.height);
        // 获取当前事件
        Event evt = Event.current;

        // 根据事件类型处理
        switch (evt.type)
        {
            case EventType.DragUpdated:
                // 如果鼠标在拖拽区域内
                if (dropArea.Contains(evt.mousePosition))
                {
                    // 检查拖拽对象是否有效
                    bool containsValidFiles = DragAndDrop.objectReferences.Any(IsValidDragObject);
                    // 根据有效性设置拖拽视觉效果
                    DragAndDrop.visualMode = containsValidFiles ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                    // 标记事件已处理
                    evt.Use();
                }
                break;

            case EventType.DragPerform:
                // 如果鼠标在拖拽区域内
                if (dropArea.Contains(evt.mousePosition))
                {
                    // 接受拖拽
                    DragAndDrop.AcceptDrag();
                    // 处理拖拽的对象
                    ProcessDroppedItems(DragAndDrop.objectReferences);
                    // 标记事件已处理
                    evt.Use();
                }
                break;
        }
    }

    // 验证拖拽对象是否有效
    private bool IsValidDragObject(Object obj)
    {
        // 获取对象路径
        string path = AssetDatabase.GetAssetPath(obj);
        // 检查对象是否是FBX文件或文件夹
        return (obj is GameObject && Path.GetExtension(path).ToLower() == ".fbx") ||
               (obj is DefaultAsset && Directory.Exists(path));
    }

    // 处理拖拽的对象
    private void ProcessDroppedItems(Object[] droppedObjects)
    {
        // 遍历所有拖拽的对象
        foreach (var obj in droppedObjects)
        {
            // 获取对象路径
            string path = AssetDatabase.GetAssetPath(obj);
            
            // 如果是FBX文件
            if (obj is GameObject && Path.GetExtension(path).ToLower() == ".fbx")
            {
                // 添加到FBX列表
                AddFBXEntry(obj, true);
            }
            // 如果是文件夹
            else if (obj is DefaultAsset && Directory.Exists(path))
            {
                // 获取项目数据路径
                string dataPath = Application.dataPath;
                // 获取文件夹完整路径
                string folderFullPath = Path.GetFullPath(path);
                
                // 获取文件夹内所有FBX文件（包括子目录）
                var fbxFiles = Directory.GetFiles(folderFullPath, "*.fbx", SearchOption.AllDirectories)
                    .Where(p => !p.EndsWith(".meta")); // 排除meta文件

                // 遍历所有FBX文件
                foreach (var file in fbxFiles)
                {
                    // 转换为Unity相对路径
                    string relativePath = "Assets" + file.Substring(dataPath.Length);
                    // 加载FBX对象
                    var fbxObj = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
                    // 如果加载成功，添加到列表
                    if (fbxObj != null) AddFBXEntry(fbxObj, true);
                }
            }
        }
    }

    // 处理FBX文件夹
    private void ProcessFBXFolder(string folderPath)
    {
        // 获取文件夹内所有FBX文件（包括子目录）
        var fbxFiles = Directory.GetFiles(folderPath, "*.fbx", SearchOption.AllDirectories)
            .Where(p => !p.EndsWith(".meta")); // 排除meta文件

        // 遍历所有FBX文件
        foreach (var file in fbxFiles)
        {
            // 转换为Unity相对路径
            string relativePath = "Assets" + file.Substring(Application.dataPath.Length);
            // 加载FBX对象
            var fbxObj = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
            // 如果加载成功，添加到列表
            if (fbxObj != null) AddFBXEntry(fbxObj, true);
        }
    }

    // 使用当前选中的对象初始化窗口
    private void InitializeWithSelection()
    {
        // 获取选中的GameObject（仅限Assets）
        var selectedObjects = Selection.GetFiltered(typeof(GameObject), SelectionMode.Assets)
            .Where(x => Path.GetExtension(AssetDatabase.GetAssetPath(x)).ToLower() == ".fbx") // 只选择FBX文件
            .ToArray();

        // 遍历所有选中的对象
        foreach (var obj in selectedObjects)
        {
            // 添加到FBX列表
            AddFBXEntry(obj, true);
        }
    }

    // 绘制工具栏
    private void DrawToolbar()
    {
        // 开始水平布局
        EditorGUILayout.BeginHorizontal();
        {
            // 添加FBX按钮
            if (GUILayout.Button("Add FBX", GUILayout.Width(100)))
            {
                // 打开文件选择对话框
                var paths = EditorUtility.OpenFilePanelWithFilters("Select FBX Files", lastUsedPath, 
                    new[] { "FBX Files", "fbx" }); // 只显示FBX文件
                
                // 如果选择了文件
                if (!string.IsNullOrEmpty(paths))
                {
                    // 记录路径
                    lastUsedPath = Path.GetDirectoryName(paths);
                    // 转换为Unity相对路径
                    string relativePath = "Assets" + paths.Substring(Application.dataPath.Length);
                    // 加载FBX对象
                    var obj = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
                    // 如果加载成功，添加到列表
                    if (obj != null) AddFBXEntry(obj, true);
                }
            }

            // 添加文件夹按钮
            if (GUILayout.Button("Add Folder", GUILayout.Width(100)))
            {
                // 打开文件夹选择对话框
                var path = EditorUtility.OpenFolderPanel("Select Folder with FBX Files", lastUsedPath, "");
                // 如果选择了文件夹
                if (!string.IsNullOrEmpty(path))
                {
                    // 记录路径
                    lastUsedPath = path;
                    // 处理文件夹中的FBX文件
                    ProcessFBXFolder(path);
                }
            }

            // 清空所有按钮
            if (GUILayout.Button("Clear All", GUILayout.Width(100)))
            {
                // 清空FBX列表
                fbxEntries.Clear();
            }

            // 只有当列表不为空时才启用提取按钮
            GUI.enabled = fbxEntries.Count > 0;
            // 提取所有动画按钮
            if (GUILayout.Button("Extract All", GUILayout.Width(100)))
            {
                // 提取所有动画
                ExtractAllAnimations();
            }
            // 恢复GUI启用状态
            GUI.enabled = true;
        }
        // 结束水平布局
        EditorGUILayout.EndHorizontal();
    }

    // 绘制FBX列表
    private void DrawFBXList()
    {
        // 开始滚动视图
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        {
            // 遍历所有FBX条目
            for (int i = 0; i < fbxEntries.Count; i++)
            {
                // 开始垂直布局（带边框样式）
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    // 开始水平布局
                    EditorGUILayout.BeginHorizontal();
                    {
                        // 显示FBX对象字段
                        fbxEntries[i].fbxObject = EditorGUILayout.ObjectField(
                            fbxEntries[i].fbxObject, typeof(GameObject), false) as GameObject;

                        // 删除按钮
                        if (GUILayout.Button("×", GUILayout.Width(20)))
                        {
                            // 从列表中移除当前条目
                            fbxEntries.RemoveAt(i);
                            // 结束当前布局
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.EndVertical();
                            // 跳出循环
                            break;
                        }
                    }
                    // 结束水平布局
                    EditorGUILayout.EndHorizontal();

                    // 如果FBX对象不为空
                    if (fbxEntries[i].fbxObject != null)
                    {
                        // 增加缩进级别
                        EditorGUI.indentLevel++;
                        
                        // 开始水平布局
                        EditorGUILayout.BeginHorizontal();
                        {
                            // 显示输出路径字段
                            fbxEntries[i].outputPath = EditorGUILayout.TextField("Output Path", fbxEntries[i].outputPath);
                            // 路径选择按钮
                            if (GUILayout.Button("...", GUILayout.Width(30)))
                            {
                                // 获取完整路径
                                string fullPath = Path.Combine(Application.dataPath, fbxEntries[i].outputPath.Replace("Assets/", ""));
                                // 打开文件夹选择对话框
                                string newPath = EditorUtility.SaveFolderPanel("Select Output Folder", fullPath, "");
                                // 如果选择了文件夹
                                if (!string.IsNullOrEmpty(newPath))
                                {
                                    // 更新输出路径
                                    fbxEntries[i].outputPath = "Assets" + newPath.Substring(Application.dataPath.Length);
                                }
                            }
                        }
                        // 结束水平布局
                        EditorGUILayout.EndHorizontal();

                        // 获取动画剪辑列表
                        var clips = fbxEntries[i].animationClips;
                        // 如果有动画剪辑
                        if (clips != null && clips.Count > 0)
                        {
                            // 显示标题
                            EditorGUILayout.LabelField("Animation Clips:", EditorStyles.boldLabel);
                            // 遍历所有动画剪辑
                            foreach (var clip in clips)
                            {
                                // 开始水平布局
                                EditorGUILayout.BeginHorizontal();
                                {
                                    // 显示复选框和剪辑名称
                                    clip.include = EditorGUILayout.ToggleLeft(clip.clipName, clip.include, GUILayout.Width(200));
                                }
                                // 结束水平布局
                                EditorGUILayout.EndHorizontal();
                            }
                        }
                        else
                        {
                            // 如果没有动画剪辑，显示提示信息
                            EditorGUILayout.HelpBox("No animation clips found in this FBX", MessageType.Info);
                        }

                        // 恢复缩进级别
                        EditorGUI.indentLevel--;
                    }
                }
                // 结束垂直布局
                EditorGUILayout.EndVertical();
                // 添加一些空白间距
                EditorGUILayout.Space();
            }
        }
        // 结束滚动视图
        EditorGUILayout.EndScrollView();
    }

    // 添加FBX条目
    private void AddFBXEntry(Object fbxObject, bool autoLoadClips = false)
    {
        // 如果对象为空则返回
        if (fbxObject == null) return;

        // 获取对象路径
        var path = AssetDatabase.GetAssetPath(fbxObject);
        // 如果不是FBX文件则返回
        if (Path.GetExtension(path).ToLower() != ".fbx") return;

        // 创建新的FBX条目
        var entry = new FBXEntry
        {
            fbxObject = fbxObject as GameObject, // FBX对象
            outputPath = Path.GetDirectoryName(path), // 输出路径默认为FBX所在目录
            animationClips = new List<ClipInfo>() // 动画剪辑列表
        };

        // 检查是否已存在相同路径的条目
        if (!fbxEntries.Any(x => AssetDatabase.GetAssetPath(x.fbxObject) == path))
        {
            // 添加到列表
            fbxEntries.Add(entry);
            // 如果需要自动加载剪辑
            if (autoLoadClips)
            {
                // 加载动画剪辑
                LoadClipsForEntry(entry);
            }
        }
    }

    // 为条目加载动画剪辑
    private void LoadClipsForEntry(FBXEntry entry)
    {
        // 如果FBX对象为空则返回
        if (entry.fbxObject == null) return;

        // 获取FBX路径
        var path = AssetDatabase.GetAssetPath(entry.fbxObject);
        // 加载路径下所有动画剪辑
        var allClips = AssetDatabase.LoadAllAssetsAtPath(path)
            .Where(x => x is AnimationClip) // 只选择AnimationClip类型
            .Cast<AnimationClip>() // 转换为AnimationClip
            .Where(x => !x.name.StartsWith("preview_") && // 排除预览剪辑
                       !x.name.StartsWith("__preview__") && 
                       !x.name.EndsWith("_preview"))
            .ToList(); // 转换为列表

        // 创建ClipInfo列表
        entry.animationClips = allClips.Select(clip => new ClipInfo
        {
            clip = clip, // 动画剪辑
            clipName = clip.name, // 剪辑名称
            include = true // 默认包含
        }).ToList();
    }

    // 提取所有动画
    private void ExtractAllAnimations()
    {
        try
        {
            // 开始批量资源编辑（提高性能）
            AssetDatabase.StartAssetEditing();

            // 计数器
            int totalClips = 0;
            // 遍历所有FBX条目
            foreach (var entry in fbxEntries)
            {
                // 如果条目无效则跳过
                if (entry.fbxObject == null || entry.animationClips == null) continue;

                // 如果输出目录不存在则创建
                if (!Directory.Exists(entry.outputPath))
                {
                    Directory.CreateDirectory(entry.outputPath);
                }

                // 遍历所有动画剪辑
                foreach (var clipInfo in entry.animationClips)
                {
                    // 如果未选中则跳过
                    if (!clipInfo.include) continue;

                    // 创建新的动画剪辑
                    var newClip = new AnimationClip();
                    // 复制属性
                    EditorUtility.CopySerialized(clipInfo.clip, newClip);

                    // 生成剪辑名称（FBX名称_剪辑名称）
                    var clipName = $"{entry.fbxObject.name}_{clipInfo.clipName}".Replace(" ", "_");
                    // 生成资源路径
                    var assetPath = $"{entry.outputPath}/{clipName}.anim";
                    // 确保路径唯一
                    assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

                    // 创建资源
                    AssetDatabase.CreateAsset(newClip, assetPath);
                    // 增加计数器
                    totalClips++;
                }
            }

            // 显示完成对话框
            EditorUtility.DisplayDialog("Success", 
                $"Extracted {totalClips} animation clips from {fbxEntries.Count} FBX files", 
                "OK");
        }
        finally
        {
            // 结束批量资源编辑
            AssetDatabase.StopAssetEditing();
            // 刷新资源数据库
            AssetDatabase.Refresh();
        }
    }

    // FBX条目类
    private class FBXEntry
    {
        public GameObject fbxObject; // FBX游戏对象
        public string outputPath; // 输出路径
        public List<ClipInfo> animationClips; // 动画剪辑列表
    }

    // 动画剪辑信息类
    private class ClipInfo
    {
        public AnimationClip clip; // 动画剪辑引用
        public string clipName; // 剪辑名称
        public bool include; // 是否包含
    }
}