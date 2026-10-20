using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("运镜速度")]
    public float zoomSpeed = 3f;

    public Transform FocusedTarget { get; private set; }

    private Camera cam;
    private float defaultSize;
    private Vector3 defaultPos;
    
    private float targetSize;
    private Vector3 targetPos;
    private bool isZooming = false;

    void Start()
    {
        cam = GetComponent<Camera>();
        // 记住游戏刚开始时，全景的大小和位置
        defaultSize = cam.orthographicSize;
        defaultPos = transform.position;
        
        targetSize = defaultSize;
        targetPos = defaultPos;
    }

    void Update()
    {
        // 测试用：按鼠标右键，可以随时退回全景！
        if (Input.GetMouseButtonDown(1))
        {
            ResetCamera();
        }

        if (isZooming)
        {
            // 核心魔法：Mathf.Lerp 和 Vector3.Lerp 可以让数值像丝般顺滑地过渡
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, zoomSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPos, zoomSpeed * Time.deltaTime);
            
            // 差不多到了就停下，节省性能
            if (Mathf.Abs(cam.orthographicSize - targetSize) < 0.01f && Vector3.Distance(transform.position, targetPos) < 0.01f)
            {
                isZooming = false;
            }
        }
    }

    // 公开方法：放大聚焦到某个目标
    public void FocusOn(Transform target, float size)
    {
        if (target == null) return;
        FocusedTarget = target;
        targetSize = size;
        // 保持Z轴（通常是-10）不变，只移动X和Y，否则画面会消失！
        targetPos = new Vector3(target.position.x, target.position.y, defaultPos.z); 
        isZooming = true;
    }

    // 公开方法：恢复原状
    public void ResetCamera()
    {
        FocusedTarget = null;
        targetSize = defaultSize;
        targetPos = defaultPos;
        isZooming = true;
    }
}
