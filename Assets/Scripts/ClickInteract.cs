using UnityEngine;

public class ClickInteract : MonoBehaviour
{
    [Header("游标设置")]
    public Texture2D hoverCursor;

    [Header("交互设置")]
    public Transform standPoint; // 主角走去哪
    
    [Header("运镜聚焦设置")]
    public Transform zoomTarget; // 走到后，摄像机放大看哪？
    public float zoomSize = 2.5f; // 数字越小，放得越大 (全景一般是5)

    private PlayerMovement player;

    void Start()
    {
        player = Object.FindFirstObjectByType<PlayerMovement>();
    }

    void OnMouseEnter()
    {
        if (hoverCursor != null) Cursor.SetCursor(hoverCursor, Vector2.zero, CursorMode.Auto);
    }

    void OnMouseExit()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    void OnMouseDown()
    {
        if (QuickFinishGame.BlocksLegacyClick()) return;
        if (player != null && standPoint != null)
        {
            // 呼叫主角：走到 standPoint，然后聚焦看 zoomTarget，缩放尺寸为 zoomSize！
            player.WalkTo(standPoint.position, zoomTarget, zoomSize);
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); 
        }
    }
}
