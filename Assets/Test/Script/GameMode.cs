using UnityEngine;

public class GameMode : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab; // 角色预制体引用
    private GameObject playerStart; // PlayerStart对象引用

    private void Awake()
    {
        // 查找PlayerStart对象
        playerStart = GameObject.Find("PlayerStart");

        if (playerStart == null)
        {
            Debug.LogError("未找到名为PlayerStart的对象！");
            return;
        }

        // 检查预制体是否已赋值
        if (playerPrefab == null)
        {
            Debug.LogError("未分配角色预制体！");
            return;
        }
        else
        {
            // 在PlayerStart位置实例化角色
            Instantiate(playerPrefab, playerStart.transform.position, playerStart.transform.rotation);
        }

    }
}