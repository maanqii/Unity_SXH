using UnityEngine;
using UnityEngine.SceneManagement; 

public class SceneChanger : MonoBehaviour
{
    [Header("游标设置")]
    public Texture2D hoverCursor; // 拖入你的交互游标 (CURSOR2)

    [Header("场景跳转")]
    public string sceneName; // 填入你要跳转的场景名字

    void OnMouseEnter()
    {
        // 鼠标进入方框范围：换成交互游标
        if (hoverCursor != null)
        {
            Cursor.SetCursor(hoverCursor, Vector2.zero, CursorMode.Auto);
        }
    }

    void OnMouseExit()
    {
        // 鼠标离开方框范围：恢复默认游标
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    void OnMouseDown()
    {
        // 点击时：先恢复默认游标，然后再跳转场景
        // (如果不恢复，跳转到新场景后鼠标可能会卡在交互图标的样子)
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        
        // 执行跳转
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning("场景名字没填！请在 Inspector 里填入 sceneName");
        }
    }

    // 保险措施：如果这个面板因为某些原因突然被隐藏或销毁，确保游标能重置
    void OnDisable()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}