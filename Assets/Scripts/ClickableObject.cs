using UnityEngine;

public class ClickableObject : MonoBehaviour
{
    public Texture2D hoverCursor; // 在面板中拖入你的悬停光标图片
    private Vector2 cursorHotspot;

    void Start()
    {
        // 设置光标的点击判定点，通常是左上角 (0,0) 或者图片中心 (width/2, height/2)
        cursorHotspot = new Vector2(hoverCursor.width / 2f, hoverCursor.height / 2f); 
    }

    // 当鼠标进入物体的碰撞体范围时
    void OnMouseEnter()
    {
        Cursor.SetCursor(hoverCursor, cursorHotspot, CursorMode.Auto);
    }

    // 当鼠标离开物体的碰撞体范围时
    void OnMouseExit()
    {
        // 恢复默认光标（传 null 即可）
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}