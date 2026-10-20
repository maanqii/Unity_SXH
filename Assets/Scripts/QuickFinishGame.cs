using System.Collections;
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

    [Header("Clue interaction: walk, zoom, reveal")]
    [Min(0.5f)] public float clueStandDistance = 1.25f;
    [Min(2f)] public float clueZoomSize = 3.6f;
    [Min(0.1f)] public float clueZoomWait = 1.1f;

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
    bool clueSequenceBusy;
    GameObject clueCard;
    Text clueTitle;
    Text clueBody;
    Transform clueFocus;

    bool ClueCardVisible => clueCard != null && clueCard.activeSelf;

    void Awake() { active = this; }
    void OnDestroy() { if (active == this) active = null; }
    void Start()
    {
        if (finalPoster != null) finalPoster.enabled = false;
        if (buyButton != null) buyButton.onClick.AddListener(BuyDrink);
        if (bubbleButton != null) bubbleButton.onClick.AddListener(CompletePoster);
        CreateClueCard();
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
        if (active.clueSequenceBusy || active.ClueCardVisible) return true;
        return active.consumedFrame == Time.frameCount || active.OverUI() || active.PointAtMouse(out _) != null;
    }

    void Update()
    {
        UpdateNPC();
        bool canBuy = vending != null && cameraController != null &&
            cameraController.FocusedTarget != null && cameraController.FocusedTarget == vending.zoomTarget;
        if (buyButton != null) buyButton.gameObject.SetActive(canBuy && !Completed && !clueSequenceBusy && !ClueCardVisible);
        if (messageText != null && Time.unscaledTime > messageUntil) messageText.text = "";
        // Right click can close the clue card as well as reset the existing camera.
        if (ClueCardVisible && Input.GetMouseButtonDown(1)) CloseClueCard();
        if (clueSequenceBusy || ClueCardVisible) return;
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
        if (Completed || point == null || clueSequenceBusy || ClueCardVisible) return;

        // Coins are pickups, not clues: keep the original instant collection.
        if (point.kind == QuickFinishPoint.Kind.Coin)
        {
            if (collectedCoins.Add(point.coinId))
            {
                Coins++;
                point.gameObject.SetActive(false);
                Say("Coin collected.");
            }
            Refresh();
            return;
        }

        if (point.kind == QuickFinishPoint.Kind.Poster && posterClue)
        {
            Say("Poster clue already collected."); return;
        }
        if (point.kind == QuickFinishPoint.Kind.Graffiti && graffitiClue)
        {
            Say("Graffiti clue already collected."); return;
        }
        if (point.kind == QuickFinishPoint.Kind.NPC && npcClue)
        {
            Say("NPC: Thank you again!"); return;
        }

        // A clue is NEVER granted on the initial click: first walk, then focus,
        // then show the clue card and update quest progress.
        StartCoroutine(WalkZoomReveal(point));
    }

    IEnumerator WalkZoomReveal(QuickFinishPoint point)
    {
        if (player == null || cameraController == null)
        {
            Say("Clue interaction needs a Player and Camera Controller.");
            yield break;
        }

        clueSequenceBusy = true;
        Vector3 target = point.transform.position;
        if (point.kind == QuickFinishPoint.Kind.NPC && npc != null)
        {
            var npcSprite = npc.GetComponentInChildren<SpriteRenderer>();
            target = npcSprite != null ? npcSprite.bounds.center : npc.position;
        }

        // PlayerMovement is intentionally horizontal-only in this project.
        // Stop just to the left/right of the hotspot, without touching its script.
        float direction = player.transform.position.x <= target.x ? -1f : 1f;
        float walkX = target.x + direction * clueStandDistance;
        player.WalkTo(new Vector3(walkX, player.transform.position.y, player.transform.position.z));
        Say("Walking to inspect...", float.PositiveInfinity);

        // Wait for the real movement, rather than using a guessed walking duration.
        float deadline = Time.time + 45f;
        while (Mathf.Abs(player.transform.position.x - walkX) > 0.08f)
        {
            if (Time.time > deadline)
            {
                clueSequenceBusy = false;
                Say("Could not reach the clue. Check PlayerMovement speed.");
                yield break;
            }
            yield return null;
        }

        if (clueFocus == null)
        {
            var focus = new GameObject("Clue Zoom Focus (runtime only)");
            focus.transform.SetParent(transform, false);
            clueFocus = focus.transform;
        }

        // Keep the character and the inspected object visible in the zoom.
        clueFocus.position = new Vector3(
            (target.x + player.transform.position.x) * 0.5f,
            (target.y + player.transform.position.y) * 0.5f,
            target.z);
        float size = Mathf.Max(clueZoomSize, Mathf.Abs(target.y - player.transform.position.y) * 0.5f + 1.4f);
        cameraController.FocusOn(clueFocus, size);
        Say("Inspecting...", float.PositiveInfinity);
        yield return new WaitForSeconds(clueZoomWait);

        string heading;
        string detail;
        switch (point.kind)
        {
            case QuickFinishPoint.Kind.Poster:
                posterClue = true;
                heading = "CLUE 01 / STATION POSTER";
                detail = "Every voice belongs on this wall.\n\nClue collected (1/3).";
                break;
            case QuickFinishPoint.Kind.Graffiti:
                graffitiClue = true;
                heading = "CLUE 02 / STREET GRAFFITI";
                detail = "Bring colour to the neighbourhood.\n\nClue collected (2/3).";
                break;
            case QuickFinishPoint.Kind.NPC:
                if (!HasDrink)
                {
                    heading = "TALK TO THE BOY";
                    detail = "Could you buy me a drink from the vending machine?\n\nCollect two coins and come back with a drink.";
                }
                else
                {
                    HasDrink = false;
                    npcClue = true;
                    heading = "CLUE 03 / THE BOY";
                    detail = "Thanks! Our voices and colours make this place home.\n\nClue collected (3/3).";
                }
                break;
            default:
                clueSequenceBusy = false;
                yield break;
        }

        Refresh();
        ShowClueCard(heading, detail);
        Say(heading + " - " + detail.Replace("\n", " "), float.PositiveInfinity);
        clueSequenceBusy = false;
    }

    // Create the clue popup at runtime so that already-built Unity scenes do not
    // need to be rebuilt and the user only has to update this one script.
    void CreateClueCard()
    {
        Canvas canvas = statusText != null ? statusText.canvas : null;
        if (canvas == null)
        {
            Debug.LogWarning("Quick Finish: clue card UI needs the existing Status Text Canvas.", this);
            return;
        }

        var card = new GameObject("Clue Card (runtime)", typeof(RectTransform), typeof(Image));
        clueCard = card;
        var rect = (RectTransform)card.transform;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(710, 310);
        var background = card.GetComponent<Image>();
        background.color = new Color(0.055f, 0.16f, 0.19f, 0.97f);

        clueTitle = CardText(rect, "Clue Title", 31, FontStyle.Bold, new Vector2(0, 93), new Vector2(640, 75));
        clueBody = CardText(rect, "Clue Description", 25, FontStyle.Normal, new Vector2(0, 2), new Vector2(640, 128));
        var buttonRoot = new GameObject("Continue Exploring", typeof(RectTransform), typeof(Image), typeof(Button));
        var buttonRect = (RectTransform)buttonRoot.transform;
        buttonRect.SetParent(rect, false);
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = Vector2.one * 0.5f;
        buttonRect.anchoredPosition = new Vector2(0, -115);
        buttonRect.sizeDelta = new Vector2(275, 52);
        var buttonImage = buttonRoot.GetComponent<Image>();
        buttonImage.color = new Color(0.35f, 0.77f, 0.75f, 1f);
        var button = buttonRoot.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(CloseClueCard);
        var buttonLabel = CardText(buttonRect, "Label", 23, FontStyle.Bold, Vector2.zero, buttonRect.sizeDelta);
        buttonLabel.text = "CONTINUE";
        buttonLabel.color = new Color(0.04f, 0.14f, 0.17f);
        card.SetActive(false);
    }

    Text CardText(RectTransform parent, string name, int fontSize, FontStyle style, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one * 0.5f;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        var label = go.GetComponent<Text>();
        label.font = messageText != null && messageText.font != null
            ? messageText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = new Color(0.96f, 0.96f, 0.90f);
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        return label;
    }

    void ShowClueCard(string title, string body)
    {
        if (clueCard == null) return;
        clueTitle.text = title;
        clueBody.text = body;
        clueCard.transform.SetAsLastSibling();
        clueCard.SetActive(true);
    }

    public void CloseClueCard()
    {
        if (clueCard != null) clueCard.SetActive(false);
        if (cameraController != null) cameraController.ResetCamera();
        if (messageText != null) messageText.text = "";
        messageUntil = 0f;
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
