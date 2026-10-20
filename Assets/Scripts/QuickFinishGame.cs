using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class QuickFinishGame : MonoBehaviour
{
    public PlayerMovement player;
    public Camera worldCamera;
    public CameraController cameraController;
    public ClickInteract vending;
    [Tooltip("Drag the intended NPC here. No character is guessed automatically.")]
    public Transform npc;
    public QuickFinishPoint npcPoint;
    public BoxCollider2D ground;
    public SpriteRenderer finalPoster;
    public Text statusText;
    public Text messageText;
    public Button buyButton;
    public Button bubbleButton;
    public Vector3 bubbleOffset = new Vector3(0, 2.8f, 0);

    public int Coins { get; private set; }
    public bool HasDrink { get; private set; }
    public bool Completed { get; private set; }
    public int Clues => (posterClue ? 1 : 0) + (graffitiClue ? 1 : 0) + (npcClue ? 1 : 0);
    bool posterClue, graffitiClue, npcClue;
    readonly HashSet<int> collectedCoins = new HashSet<int>();
    static QuickFinishGame active;
    int consumedFrame = -1;
    float messageUntil;
    readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    void Awake() { active = this; }
    void OnDestroy() { if (active == this) active = null; }
    void Start()
    {
        if (finalPoster != null) finalPoster.enabled = false;
        if (buyButton != null) buyButton.onClick.AddListener(BuyDrink);
        if (bubbleButton != null) bubbleButton.onClick.AddListener(CompletePoster);
        Refresh();
        Say("Explore the poster and graffiti. Collect two coins.");
        if (player == null || worldCamera == null || vending == null)
            Debug.LogWarning("Quick Finish: missing Player, Camera or Vending reference.", this);
        if (npc == null) Debug.LogWarning("Quick Finish: assign the intended NPC to QuickFinishGame.npc.", this);
    }

    bool OverUI()
    {
        if (EventSystem.current == null) return false;
        uiHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Input.mousePosition }, uiHits);
        return uiHits.Count > 0;
    }

    QuickFinishPoint PointAtMouse(out bool legacy)
    {
        legacy = false;
        if (worldCamera == null) return null;
        Vector2 position = worldCamera.ScreenToWorldPoint(Input.mousePosition);
        QuickFinishPoint selected = null;
        foreach (var hit in Physics2D.OverlapPointAll(position))
        {
            var point = hit.GetComponentInParent<QuickFinishPoint>();
            if (point != null && point.game == this && point.isActiveAndEnabled)
            {
                // Coins win when they overlap a larger interaction area.
                if (selected == null || point.kind == QuickFinishPoint.Kind.Coin) selected = point;
            }
            if (hit.GetComponentInParent<ClickInteract>() != null ||
                hit.GetComponentInParent<SceneChanger>() != null ||
                hit.GetComponentInParent<HoverInteract>() != null) legacy = true;
        }
        return selected;
    }

    // Called by existing OnMouseDown handlers as well as our Update: independent of callback order.
    public static bool BlocksLegacyClick()
    {
        if (active == null || !active.isActiveAndEnabled) return false;
        return active.consumedFrame == Time.frameCount || active.OverUI() || active.PointAtMouse(out _) != null;
    }

    void Update()
    {
        UpdateNPC();
        bool canBuy = vending != null && cameraController != null &&
            cameraController.FocusedTarget != null && cameraController.FocusedTarget == vending.zoomTarget;
        if (buyButton != null) buyButton.gameObject.SetActive(canBuy && !Completed);
        if (messageText != null && Time.unscaledTime > messageUntil) messageText.text = "";
        if (!Input.GetMouseButtonDown(0) || worldCamera == null || OverUI()) return;
        var point = PointAtMouse(out bool legacy);
        if (point != null)
        {
            consumedFrame = Time.frameCount;
            point.Interact();
            return;
        }
        if (legacy) return; // Preserve the existing vending walk/focus callback.
        Vector2 position = worldCamera.ScreenToWorldPoint(Input.mousePosition);
        if (ground != null && ground.OverlapPoint(position) && player != null)
            player.WalkTo(position);
    }

    void UpdateNPC()
    {
        if (npcPoint == null) return;
        npcPoint.gameObject.SetActive(npc != null && npc.gameObject.activeInHierarchy);
        if (npc == null) return;
        var renderer = npc.GetComponentInChildren<SpriteRenderer>();
        var box = npcPoint.GetComponent<BoxCollider2D>();
        npcPoint.transform.position = renderer != null ? renderer.bounds.center : npc.position;
        if (renderer != null && box != null) box.size = renderer.bounds.size;
    }

    void LateUpdate()
    {
        if (bubbleButton == null || !bubbleButton.gameObject.activeSelf || player == null || worldCamera == null) return;
        var renderer = player.GetComponent<SpriteRenderer>();
        Vector3 head = renderer != null ? new Vector3(renderer.bounds.center.x, renderer.bounds.max.y + 0.5f, player.transform.position.z) : player.transform.position + bubbleOffset;
        Vector3 screen = worldCamera.WorldToScreenPoint(head);
        screen.x = Mathf.Clamp(screen.x, 70, Screen.width - 70);
        screen.y = Mathf.Clamp(screen.y, 40, Screen.height - 40);
        bubbleButton.transform.position = screen;
    }

    public void Interact(QuickFinishPoint point)
    {
        if (Completed || point == null) return;
        switch (point.kind)
        {
            case QuickFinishPoint.Kind.Poster:
                if (posterClue) { Say("Poster clue already collected."); break; }
                posterClue = true; Say("Clue 1: Every voice belongs on this wall."); break;
            case QuickFinishPoint.Kind.Graffiti:
                if (graffitiClue) { Say("Graffiti clue already collected."); break; }
                graffitiClue = true; Say("Clue 2: Bring colour to the neighbourhood."); break;
            case QuickFinishPoint.Kind.Coin:
                if (collectedCoins.Add(point.coinId))
                {
                    Coins++; point.gameObject.SetActive(false); Say("Coin collected.");
                }
                break;
            case QuickFinishPoint.Kind.NPC:
                if (npcClue) { Say("NPC: Thank you again!"); break; }
                if (!HasDrink) { Say("NPC: Could you buy me a drink from the vending machine?"); break; }
                HasDrink = false; npcClue = true;
                Say("NPC: Thanks! Clue 3: Our voices and colours make this place home."); break;
        }
        Refresh();
    }

    public void BuyDrink()
    {
        if (Completed || vending == null || cameraController == null || vending.zoomTarget == null || cameraController.FocusedTarget != vending.zoomTarget) return;
        if (HasDrink || npcClue) { Say("You have already purchased the drink."); return; }
        if (Coins < 2) { Say("You need 2 coins to buy a drink."); return; }
        Coins -= 2; HasDrink = true; Say("Drink purchased. Give it to the NPC."); Refresh();
    }

    public void CompletePoster()
    {
        if (Completed || Clues != 3) return;
        if (finalPoster == null) { Say("Assign the Final Poster renderer in the Inspector."); return; }
        Completed = true; finalPoster.enabled = true;
        if (cameraController != null) cameraController.ResetCamera();
        Refresh(); Say("Poster Completed", float.PositiveInfinity);
    }

    void Refresh()
    {
        string task = Completed ? "Poster Completed" : Clues == 3 ? "Click the bubble above your head." :
            !posterClue ? "Inspect the station poster." : !graffitiClue ? "Inspect the wall graffiti." :
            HasDrink ? "Give the drink to the NPC." : "Collect 2 coins; use the vending machine (right-click to exit).";
        if (statusText != null) statusText.text = $"Clues {Clues}/3    Coins {Coins}/2    Drink: {(HasDrink ? "Yes" : "No")}\n{task}";
        if (bubbleButton != null) bubbleButton.gameObject.SetActive(Clues == 3 && !Completed);
    }

    void Say(string message, float seconds = 6f)
    {
        if (messageText != null) messageText.text = message;
        messageUntil = Time.unscaledTime + seconds;
    }
}
