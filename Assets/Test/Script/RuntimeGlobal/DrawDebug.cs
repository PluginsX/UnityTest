using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class DrawDebug
{
    private static Material _lineMaterial;
    // 初始化材质（兼容URP/HDRP）
    private static Material GetLineMaterial(Color SolidColor = default)
    {
        if (_lineMaterial == null)
        {
            // 使用兼容性更高的Shader（避免Sprites/Default在URP/HDRP中失效）
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? 
                           Shader.Find("Unlit/Color") ?? 
                           Shader.Find("Sprites/Default");
            _lineMaterial = new Material(shader);
            _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            _lineMaterial.color = SolidColor;
        }
        return _lineMaterial;
    }

    #region 基础参数
    private static Material _debugMaterial;
    private static Material DebugMaterial
    {
        get
        {
            if (_debugMaterial == null)
            {
                _debugMaterial = new Material(Shader.Find("UI/Default"));
                _debugMaterial.hideFlags = HideFlags.HideAndDontSave;
                _debugMaterial.color = Color.white;
            }
            return _debugMaterial;
        }
    }
    #endregion


    #region 1. 绘制线段
    public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration=0,float width=0.01f)
    {
        GameObject go = new GameObject("DebugLine");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.startColor = lr.endColor = color;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startWidth = lr.endWidth = width;
        lr.useWorldSpace = true;
        if(duration > 0)
            Object.Destroy(go, duration);
    }
    #endregion

    #region 2. 绘制球体（参数控制边数）
    public static void DrawSphere(Vector3 center, float radius, Color color, int segments = 16, float duration = 0,float width=0.01f)
    {
        // 绘制经线
        for (int i = 0; i < segments; i++)
        {
            float lat = Mathf.PI * i / segments;
            for (int j = 0; j < segments; j++)
            {
                float lon1 = 2 * Mathf.PI * j / segments;
                float lon2 = 2 * Mathf.PI * (j + 1) / segments;
                Vector3 p1 = center + new Vector3(
                    radius * Mathf.Sin(lat) * Mathf.Cos(lon1),
                    radius * Mathf.Cos(lat),
                    radius * Mathf.Sin(lat) * Mathf.Sin(lon1)
                );
                Vector3 p2 = center + new Vector3(
                    radius * Mathf.Sin(lat) * Mathf.Cos(lon2),
                    radius * Mathf.Cos(lat),
                    radius * Mathf.Sin(lat) * Mathf.Sin(lon2)
                );
                DrawLine(p1, p2, color, duration,width);
            }
        }
        // 绘制纬线
        for (int i = 1; i < segments; i++)
        {
            float lat1 = Mathf.PI * (i - 1) / segments;
            float lat2 = Mathf.PI * i / segments;
            for (int j = 0; j < segments; j++)
            {
                float lon = 2 * Mathf.PI * j / segments;
                Vector3 p1 = center + new Vector3(
                    radius * Mathf.Sin(lat1) * Mathf.Cos(lon),
                    radius * Mathf.Cos(lat1),
                    radius * Mathf.Sin(lat1) * Mathf.Sin(lon)
                );
                Vector3 p2 = center + new Vector3(
                    radius * Mathf.Sin(lat2) * Mathf.Cos(lon),
                    radius * Mathf.Cos(lat2),
                    radius * Mathf.Sin(lat2) * Mathf.Sin(lon)
                );
                DrawLine(p1, p2, color, duration,width);
            }
        }
    }


    #endregion

    #region 3. 绘制正方体
    public static void DrawCube(Vector3 center, Vector3 size, Color color, float duration = 0,float width=0.01f)
    {
        Vector3 half = size * 0.5f;
        Vector3[] corners = new Vector3[8];
        corners[0] = center + new Vector3(-half.x, -half.y, -half.z);
        corners[1] = center + new Vector3(half.x, -half.y, -half.z);
        corners[2] = center + new Vector3(half.x, -half.y, half.z);
        corners[3] = center + new Vector3(-half.x, -half.y, half.z);
        corners[4] = center + new Vector3(-half.x, half.y, -half.z);
        corners[5] = center + new Vector3(half.x, half.y, -half.z);
        corners[6] = center + new Vector3(half.x, half.y, half.z);
        corners[7] = center + new Vector3(-half.x, half.y, half.z);

        // 立方体的12条边
        int[,] edges = new int[,]
        {
            {0,1},{1,2},{2,3},{3,0}, // 底面
            {4,5},{5,6},{6,7},{7,4}, // 顶面
            {0,4},{1,5},{2,6},{3,7}  // 侧面
        };

        for (int i = 0; i < edges.GetLength(0); i++)
        {
            DrawLine(corners[edges[i,0]], corners[edges[i,1]], color, duration,width);
        }
    }
    #endregion

    #region 4. 绘制圆锥体（参数控制边数）
    public static void DrawConeWire(Vector3 start, Vector3 tip, float baseRadius, Color color, int segments = 12, float duration = 0.1f, float width = 0.02f)
    {
        // 圆锥轴线方向
        Vector3 axis = (tip - start).normalized;

        // 构造底面圆环的旋转，使其法线与 axis 对齐
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis);

        // 计算底面圆上的点
        Vector3[] baseCircle = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = 2 * Mathf.PI * i / segments;
            // 在局部空间生成圆环点
            Vector3 localPos = new Vector3(
                baseRadius * Mathf.Cos(angle),
                0,
                baseRadius * Mathf.Sin(angle)
            );
            // 旋转到世界空间并加上圆心
            baseCircle[i] = start + rot * localPos;
        }

        // 绘制底面圆线框
        for (int i = 0; i < segments; i++)
        {
            DrawLine(baseCircle[i], baseCircle[(i + 1) % segments], color, duration, width);
        }
        // 绘制底面圆线框到Start连线
        for (int i = 0; i < segments; i++)
        {
            DrawLine(baseCircle[i], start, color, duration, width);
        }
        // 绘制侧面线框
        for (int i = 0; i < segments; i++)
        {
            DrawLine(baseCircle[i], tip, color, duration, width);
        }
    }
    #endregion
    
    #region 5. 绘制箭头
    public static void DrawArrow(
        Vector3 start,
        Vector3 end,
        float coneBaseRadius,
        float coneHeight,
        int coneSegments,
        Color color,
        float duration = 0f,
        float width = 0.02f)
    {
        Vector3 dir = (end - start).normalized;
        float totalLength = Vector3.Distance(start, end);

        // 圆锥体底面圆心位置（箭头部分在终点，线段部分到圆锥底面）
        Vector3 coneBaseCenter = end - dir * coneHeight;

        // 绘制线段（从起点到圆锥底面圆心）
        DrawLine(start, coneBaseCenter, color, duration, width);

        // 绘制箭头（圆锥体，底面圆心到终点）
        DrawConeWire(coneBaseCenter, end, coneBaseRadius, color, coneSegments, duration, width);
    }
    #endregion

    #region 6. 绘制轴
    public static void DrawAxis(
        Vector3 origin,
        Transform transform,
        float arrowLength,
        float arrowSize,
        float duration = 0,
        float width = 0.02f)
    {
        int coneSegments = 3;
        float coneBaseRadius = arrowSize * 0.5f;
        float coneHeight = arrowSize;

        // X轴（红色）
        Vector3 xEnd = origin + transform.right * arrowLength;
        DrawArrow(origin, xEnd, coneBaseRadius, coneHeight, coneSegments, Color.red, duration, width);

        // Y轴（绿色）
        Vector3 yEnd = origin + transform.up * arrowLength;
        DrawArrow(origin, yEnd, coneBaseRadius, coneHeight, coneSegments, Color.green, duration, width);

        // Z轴（蓝色）
        Vector3 zEnd = origin + transform.forward * arrowLength;
        DrawArrow(origin, zEnd, coneBaseRadius, coneHeight, coneSegments, Color.blue, duration, width);
    }

    #endregion

    #region 5. 绘制文字
    public static void DrawText(Vector3 position, Vector3 Direction, int FontSize, string text, Color color, float duration = 0)
    {
        MonoBehaviour behaviour = GetDebugBehaviour();
        behaviour.StartCoroutine(DrawTextCoroutine(position, Direction, FontSize, text, color, duration));
    }

    public static IEnumerator DrawTextCoroutine(Vector3 position, Vector3 Direction,int FontSize,string text, Color color, float duration)
    {
        GameObject textObj = new GameObject(text);
        textObj.transform.position = position;
        textObj.transform.rotation = Quaternion.LookRotation(Vector3.forward,Direction);
        
        textObj.hideFlags = HideFlags.HideAndDontSave;

        var textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.color = color;
        textMesh.fontSize = FontSize;
        textMesh.anchor = TextAnchor.MiddleCenter;

        if (duration > 0)
            MonoBehaviour.Destroy(textObj, duration);

        yield return null;
    }
    #endregion

    #region 辅助方法
    private static MonoBehaviour GetDebugBehaviour()
    {
        // 获取一个隐藏的MonoBehaviour用于协程
        GameObject debugObj = GameObject.Find("_DebugHelper");
        if (debugObj == null)
        {
            debugObj = new GameObject("_DebugHelper");
            debugObj.hideFlags = HideFlags.HideAndDontSave;
            return debugObj.AddComponent<EmptyMonoBehaviour>();
        }
        return debugObj.GetComponent<MonoBehaviour>();
    }

    private class EmptyMonoBehaviour : MonoBehaviour { }
    #endregion
}