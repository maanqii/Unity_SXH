using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("移动速度")]
    public float speed = 3f; 

    private Vector3 targetPos;
    private bool isMoving = false;
    private Animator anim;
    private SpriteRenderer sr;
    
    // --- 新增：摄像机运镜相关的记忆变量 ---
    private CameraController camController;
    private Transform focusTarget; 
    private float focusSize;

    void Start()
    {
        targetPos = transform.position; 
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        // 自动找到场景里的摄像机控制器
        camController = Object.FindFirstObjectByType<CameraController>();
    }

    void Update()
    {
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

            if (targetPos.x > transform.position.x) sr.flipX = false;
            else if (targetPos.x < transform.position.x) sr.flipX = true;

            if (Vector3.Distance(transform.position, targetPos) < 0.05f)
            {
                isMoving = false;
                if(anim != null) anim.SetBool("isWalking", false); 
                
                // --- 新增：到达目的地后，如果记了要放大的目标，就通知摄像机放大 ---
                if (focusTarget != null && camController != null)
                {
                    camController.FocusOn(focusTarget, focusSize);
                    focusTarget = null; // 聚焦完就清空记忆，免得下次乱触发
                }
            }
        }
    }

    // --- 修改：增加接收聚焦目标和大小的参数 ---
    public void WalkTo(Vector3 destination, Transform targetToFocus = null, float zoomSize = 5f)
    {
        targetPos = destination;
        targetPos.y = transform.position.y; 
        targetPos.z = transform.position.z; 
        
        // 记下别人交代给她的放大目标和尺寸
        focusTarget = targetToFocus; 
        focusSize = zoomSize;
        
        isMoving = true;
        if(anim != null) anim.SetBool("isWalking", true); 
    }
}