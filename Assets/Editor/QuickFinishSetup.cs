using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class QuickFinishSetup
{
    const string RootName = "Quick Finish";

    static T[] InScene<T>() where T : Component => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(x => x.gameObject.scene == SceneManager.GetActiveScene()).ToArray();

    [MenuItem("Tools/Quick Finish/Setup Current Scene")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play Mode before configuring Quick Finish."); return;
        }
        var scene = SceneManager.GetActiveScene();
        var games = InScene<QuickFinishGame>();
        if (games.Length > 0)
        {
            Selection.activeGameObject = games[0].gameObject;
            Debug.Log("Quick Finish already configured. Existing layout/references preserved. Assign NPC in the Inspector; no rerun required.", games[0]);
            return;
        }
        var players = InScene<PlayerMovement>();
        var machines = InScene<ClickInteract>().Where(x => x.name == "Btn_VendingMachine").ToArray();
        var cameras = InScene<Camera>().Where(x => x.CompareTag("MainCamera")).ToArray();
        if (players.Length != 1 || machines.Length != 1 || cameras.Length != 1)
        {
            Debug.LogError("Quick Finish requires exactly one PlayerMovement, MainCamera and Btn_VendingMachine in the active scene. Open Assets/Scenes/New Scene1.unity."); return;
        }
        // Only identify a painted background using its actual source asset, not a guessed name.
        var backgrounds = InScene<SpriteRenderer>().Where(x => x.sprite != null && AssetDatabase.GetAssetPath(x.sprite) == "Assets/街道_lumen.png").ToArray();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Quick Finish");
        var root = New(RootName, null);
        SceneManager.MoveGameObjectToScene(root, scene);
        var game = Undo.AddComponent<QuickFinishGame>(root);
        game.player = players[0]; game.vending = machines[0]; game.worldCamera = cameras[0];
        game.cameraController = cameras[0].GetComponent<CameraController>();

        var poster = Point(root.transform, game, "Poster Hotspot", QuickFinishPoint.Kind.Poster, new Vector2(1.4f, 2.7f));
        var graffiti = Point(root.transform, game, "Graffiti Hotspot", QuickFinishPoint.Kind.Graffiti, new Vector2(7.8f, 3.6f));
        var coin1 = Point(root.transform, game, "Coin Vending", QuickFinishPoint.Kind.Coin, Vector2.one * 0.85f);
        var coin2 = Point(root.transform, game, "Coin Standing Sign", QuickFinishPoint.Kind.Coin, Vector2.one * 0.85f);
        coin1.coinId = 0; coin2.coinId = 1;
        var coinSprite = Placeholder("Coin", true);
        AddSprite(coin1.gameObject, coinSprite, new Vector2(0.65f, 0.65f), 20);
        AddSprite(coin2.gameObject, coinSprite, new Vector2(0.65f, 0.65f), 20);

        game.npcPoint = Point(root.transform, game, "NPC Hotspot (assign NPC on manager)", QuickFinishPoint.Kind.NPC, new Vector2(2, 3));
        game.npcPoint.gameObject.SetActive(false);
        var final = New("Final Poster (adjust wall placement)", root.transform);
        game.finalPoster = AddSprite(final, Placeholder("FinalPoster", false), new Vector2(2, 2.8f), 20);
        game.finalPoster.enabled = false;
        var ground = New("Ground Click Area", root.transform);
        ground.layer = 2; // Ignore Raycast: never steal an existing OnMouseDown callback.
        game.ground = Undo.AddComponent<BoxCollider2D>(ground);
        game.ground.isTrigger = true;
        if (backgrounds.Length == 1)
        {
            var bg = backgrounds[0];
            // Normalized source-image coordinates, measured from the inspected 4185 x 2447 image.
            poster.transform.position = ImagePoint(bg, 0.488f, 0.505f);
            graffiti.transform.position = ImagePoint(bg, 0.821f, 0.495f);
            coin1.transform.position = ImagePoint(bg, 0.714f, 0.263f);
            coin2.transform.position = ImagePoint(bg, 0.174f, 0.253f);
            final.transform.position = ImagePoint(bg, 0.558f, 0.57f);
            ground.transform.position = ImagePoint(bg, 0.51f, 0.14f);
            game.ground.size = new Vector2(bg.bounds.size.x * 0.79f, bg.bounds.size.y * 0.22f);
        }
        else
        {
            // Leave uncertain areas inactive rather than binding them to unrelated art.
            poster.gameObject.SetActive(false); graffiti.gameObject.SetActive(false); coin2.gameObject.SetActive(false);
            coin1.transform.position = game.vending.transform.position + Vector3.down * 3;
            game.ground.enabled = false;
            Debug.LogWarning("Background not uniquely identified. Position and activate Poster/Graffiti/Coin Standing Sign, position Final Poster, and enable Ground Click Area manually.", root);
        }
        CreateUI(root.transform, game);
        if (InScene<EventSystem>().Length == 0)
        {
            var events = New("EventSystem", root.transform);
            Undo.AddComponent<EventSystem>(events);
            Undo.AddComponent<StandaloneInputModule>(events); // Project activeInputHandler = Both.
        }
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = root;
        Debug.Log("Quick Finish created. Drag the intended NPC (candidate: role2_0) into NPC on this manager. Check cyan hotspot outlines and Final Poster position, then Ctrl+S. All existing art transforms and vending settings are preserved.", root);
    }

    static Vector3 ImagePoint(SpriteRenderer renderer, float u, float v)
    {
        var sprite = renderer.sprite;
        Vector2 pixels = new Vector2(sprite.texture.width * u, sprite.texture.height * v);
        Vector2 local = (pixels - sprite.rect.position - sprite.pivot) / sprite.pixelsPerUnit;
        return renderer.transform.TransformPoint(new Vector3(local.x, local.y, 0));
    }

    static GameObject New(string name, Transform parent)
    {
        var obj = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        if (parent != null) obj.transform.SetParent(parent, false);
        return obj;
    }

    static QuickFinishPoint Point(Transform parent, QuickFinishGame game, string name, QuickFinishPoint.Kind kind, Vector2 size)
    {
        var obj = New(name, parent);
        var point = Undo.AddComponent<QuickFinishPoint>(obj);
        point.game = game; point.kind = kind;
        var box = obj.GetComponent<BoxCollider2D>(); box.size = size; box.isTrigger = true;
        return point;
    }

    static SpriteRenderer AddSprite(GameObject obj, Sprite sprite, Vector2 size, int order)
    {
        var renderer = Undo.AddComponent<SpriteRenderer>(obj);
        renderer.sprite = sprite; renderer.sortingOrder = order;
        renderer.drawMode = SpriteDrawMode.Sliced; renderer.size = size;
        return renderer;
    }

    static Sprite Placeholder(string name, bool coin)
    {
        const string folder = "Assets/QuickFinishGenerated";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "QuickFinishGenerated");
        string path = folder + "/" + name + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        const int w = 64, h = 64;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float radius = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
            Color color = coin ? (radius > 30 ? Color.clear : radius > 25 ? new Color(0.8f, 0.45f, 0.05f) : new Color(1, 0.8f, 0.15f)) :
                (x < 4 || x > 59 || y < 4 || y > 59 ? new Color(0.15f, 0.3f, 0.4f) : new Color(1, 0.92f, 0.7f));
            if (!coin && x > 12 && x < 52 && ((y > 40 && y < 47) || (y > 26 && y < 32) || (y > 13 && y < 19))) color = new Color(0.2f, 0.65f, 0.65f);
            texture.SetPixel(x, y, color);
        }
        texture.Apply();
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    static Text Label(RectTransform rect, string text, int size)
    {
        var label = Undo.AddComponent<Text>(rect.gameObject);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = text; label.fontSize = size; label.color = Color.white;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        return label;
    }

    static Button Button(Transform parent, string name, string title, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = Rect(parent, name, anchor, position, size);
        var image = Undo.AddComponent<Image>(rect.gameObject); image.color = new Color(0.12f, 0.3f, 0.4f, 0.95f);
        var button = Undo.AddComponent<Button>(rect.gameObject); button.targetGraphic = image;
        var label = Label(Rect(rect, "Label", Vector2.one * 0.5f, Vector2.zero, size), title, 22);
        label.alignment = TextAnchor.MiddleCenter;
        return button;
    }

    static void CreateUI(Transform parent, QuickFinishGame game)
    {
        var obj = New("Quick Finish UI", parent);
        var canvas = Undo.AddComponent<Canvas>(obj); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        var scaler = Undo.AddComponent<CanvasScaler>(obj); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        Undo.AddComponent<GraphicRaycaster>(obj);
        var panel = Rect(obj.transform, "HUD", new Vector2(0, 1), new Vector2(16, -16), new Vector2(1000, 125));
        var image = Undo.AddComponent<Image>(panel.gameObject); image.color = new Color(0, 0, 0, 0.75f);
        game.statusText = Label(Rect(panel, "Status", new Vector2(0, 1), new Vector2(12, -8), new Vector2(975, 75)), "Clues 0/3    Coins 0/2    Drink: No", 24);
        game.messageText = Label(Rect(panel, "Message", new Vector2(0, 0), new Vector2(12, 8), new Vector2(975, 38)), "", 21);
        game.buyButton = Button(obj.transform, "Buy Drink", "Buy Drink (2 coins)", new Vector2(1, 0), new Vector2(-20, 20), new Vector2(280, 65));
        game.buyButton.gameObject.SetActive(false);
        game.bubbleButton = Button(obj.transform, "Finish Bubble", "Finish!", Vector2.one * 0.5f, Vector2.zero, new Vector2(130, 60));
        game.bubbleButton.gameObject.SetActive(false);
    }
}
