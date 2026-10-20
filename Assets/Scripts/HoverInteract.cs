using UnityEngine;

public class HoverInteract : MonoBehaviour
{
    [Header("游标设置")]
    public Texture2D hoverCursor;

    [Header("描边设置")]
    public Color outlineColor = Color.white; // 描边颜色
    public float outlineSize = 0.05f;        // 描边粗细 (如果觉得太夸张，改成 0.02f 试试)

    [Header("UI面板")]
    public GameObject optionPanel;

    private GameObject[] outlineObjects = new GameObject[4];
    private SpriteRenderer mainRenderer;

    void Start()
    {
        mainRenderer = GetComponent<SpriteRenderer>();
        if (optionPanel != null) optionPanel.SetActive(false);

        // 游戏开始时，自动生成4个方向的描边并隐藏
        Vector3[] offsets = {
            new Vector3(outlineSize, 0, 0),
            new Vector3(-outlineSize, 0, 0),
            new Vector3(0, outlineSize, 0),
            new Vector3(0, -outlineSize, 0)
        };

        for (int i = 0; i < 4; i++)
        {
            GameObject obj = new GameObject("Outline_" + i);
            obj.transform.SetParent(transform);
            obj.transform.localPosition = offsets[i];
            obj.transform.localScale = Vector3.one;

            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = mainRenderer.sprite;
            sr.color = outlineColor;
            sr.sortingLayerID = mainRenderer.sortingLayerID;
            sr.sortingOrder = mainRenderer.sortingOrder - 1; // 垫在原图下面

            obj.SetActive(false); // 强制默认隐藏
            outlineObjects[i] = obj;
        }
    }

    void OnMouseEnter()
    {
        // 鼠标放上去：换游标 + 显示描边
        if (hoverCursor != null)
            Cursor.SetCursor(hoverCursor, Vector2.zero, CursorMode.Auto);
        
        foreach (var obj in outlineObjects) obj.SetActive(true);
    }

    void OnMouseExit()
    {
        // 鼠标移开：恢复游标 + 隐藏描边
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        
        foreach (var obj in outlineObjects) obj.SetActive(false);
    }

    void OnMouseDown()
    {
        // 弹出选项面板
        if (optionPanel != null) optionPanel.SetActive(true);
        
        // 隐藏点击的这个图标自己
        gameObject.SetActive(false); 
    }
}