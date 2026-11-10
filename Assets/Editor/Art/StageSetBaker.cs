using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static StageBuilder;

// Builds the sets of the RC car and guard dogs minigames in code and saves them as prefabs
// ("Bake" only creates missing prefabs; "Rebuild" redoes one in place, losing hand edits to it).
// The elevator and drone prefabs were made the same way once and are now edited by hand only.
// Also registers all the stage prefabs in the GameManager's minigame list.
public static class StageSetBaker
{
    public const string PrefabDir = "Assets/Prefabs";
    public const string ElevatorPath = PrefabDir + "/---- FOURTH MINI GAME (elevator) ----.prefab";
    public const string PlazaPath = PrefabDir + "/---- FIFTH MINI GAME (drone plaza) ----.prefab";
    public const string NightPath = PrefabDir + "/---- SIXTH MINI GAME (drone night) ----.prefab";
    public const string RcCarPath = PrefabDir + "/---- SEVENTH MINI GAME (rc car) ----.prefab";
    public const string DogsPath = PrefabDir + "/---- EIGHTH MINI GAME (guard dogs) ----.prefab";
    private const string CarSheetPath = "Assets/bk_cars1.a.png";
    private const string MonitoPath = PrefabDir + "/Monito.prefab";

    // What the phone camera sees, in the stage's local space (measured in the game at 1920x1080)
    private static readonly Rect FullView = Rect.MinMaxRect(-2.91f, -9.806f, 2.91f, 2.806f);

    [MenuItem("Tools/Agent TR8N1/Bake code stages to prefabs")]
    public static void BakeStages()
    {
        Bake(RcCarPath, 120f, BuildRcCarRoad);
        Bake(DogsPath, 140f, BuildDogStreet);
    }

    // Rebuild a stage prefab IN PLACE with its current Build method: keeps the file, its root and the stage
    // component (so the GameManager's reference in the scene keeps working), throws away every child
    // and builds the set again. Hand edits to that prefab's children are lost.
    [MenuItem("Tools/Agent TR8N1/Rebuild guard dogs stage")]
    public static void RebuildDogStage() => RebuildInPlace(DogsPath, BuildDogStreet);

    [MenuItem("Tools/Agent TR8N1/Rebuild RC car stage")]
    public static void RebuildRcCarStage() => RebuildInPlace(RcCarPath, BuildRcCarRoad);

    private static void RebuildInPlace(string path, System.Action<GameObject> build)
    {
        ArtBaker.BakeSprites();
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        for (int i = contents.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(contents.transform.GetChild(i).gameObject);
        build(contents);
        PrefabUtility.SaveAsPrefabAsset(contents, path);
        PrefabUtility.UnloadPrefabContents(contents);
        Debug.Log("Rebuilt " + path);
    }

    // Adds the baked prefabs to the GameManager's minigame list (after the ones already there)
    [MenuItem("Tools/Agent TR8N1/Register stage prefabs in open scene")]
    public static void RegisterInOpenScene()
    {
        GameManager manager = Object.FindAnyObjectByType<GameManager>();
        if (manager == null) { Debug.LogError("No GameManager in the open scene"); return; }

        SerializedObject so = new SerializedObject(manager);
        SerializedProperty list = so.FindProperty("minigamePrefabs");
        foreach (string path in new[] { ElevatorPath, PlazaPath, NightPath, RcCarPath, DogsPath })
        {
            WarioStage stage = AssetDatabase.LoadAssetAtPath<WarioStage>(path);
            if (stage == null) { Debug.LogError("Missing " + path); continue; }
            bool present = false;
            for (int i = 0; i < list.arraySize; i++) present |= list.GetArrayElementAtIndex(i).objectReferenceValue == stage;
            if (present) continue;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = stage;
        }
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Debug.Log($"GameManager now has {manager.minigamePrefabs.Length} minigames (save the scene)");
    }

    public static void RegisterInScene(string scenePath)
    {
        EditorSceneManager.OpenScene(scenePath);
        RegisterInOpenScene();
        EditorSceneManager.SaveOpenScenes();
    }

    private static void Bake(string path, float x, System.Action<GameObject> build)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log("Kept existing " + path);
            return;
        }
        GameObject root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
        root.transform.position = new Vector3(x, 0f, 0f); // each minigame lives far from the others
        build(root);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log("Baked " + path);
    }

    private static Transform Child(Transform parent, string name, Vector3 localPosition, float angle = 0f)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        t.localRotation = Quaternion.Euler(0f, 0f, angle);
        return t;
    }

    private static void Assign(Object component, params (string field, Object value)[] refs)
    {
        SerializedObject so = new SerializedObject(component);
        foreach (var (field, value) in refs) so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject Monito => AssetDatabase.LoadAssetAtPath<GameObject>(MonitoPath);

    // ---------------- RC car (highway from above) ----------------

    // Three-lane road down the middle, curbs and grass on the sides. The lane dashes, curbs and grass
    // live under "Scrolling", which RcCarStage moves down and wraps every 2 units to make the road move.
    private static void BuildRcCarRoad(GameObject root)
    {
        RcCarStage stage = root.GetComponent<RcCarStage>();
        if (stage == null) stage = root.AddComponent<RcCarStage>();
        Transform transform = root.transform;
        const float roadHalf = 1.65f, curbWidth = 0.25f, top = 4.5f, bottom = -11.5f;
        float height = top - bottom, centerY = (top + bottom) / 2f;
        float grassInner = roadHalf + curbWidth, grassOuter = FullView.xMax + 0.3f;
        float grassW = grassOuter - grassInner, grassX = grassInner + grassW / 2f;

        // Static: asphalt and the solid edge lines
        Box(transform, "Asphalt", 0f, centerY, roadHalf * 2f, height, new Color(0.22f, 0.22f, 0.25f), -10);
        Box(transform, "Edge Line L", -roadHalf + 0.06f, centerY, 0.05f, height, new Color(0.92f, 0.92f, 0.9f), -7);
        Box(transform, "Edge Line R", roadHalf - 0.06f, centerY, 0.05f, height, new Color(0.92f, 0.92f, 0.9f), -7);

        // Scrolling: taller than the view by one period so the wrap is invisible
        Transform scroll = Child(transform, "Scrolling", Vector3.zero);
        float scrollH = height + 2f, scrollY = centerY + 1f;
        Tiled(scroll, "Grass L", GameSprites.RcGrass(), -grassX, scrollY, grassW, scrollH, -9);
        Tiled(scroll, "Grass R", GameSprites.RcGrass(), grassX, scrollY, grassW, scrollH, -9);
        Tiled(scroll, "Curb L", GameSprites.RcCurb(), -(roadHalf + curbWidth / 2f), scrollY, curbWidth, scrollH, -8);
        Tiled(scroll, "Curb R", GameSprites.RcCurb(), roadHalf + curbWidth / 2f, scrollY, curbWidth, scrollH, -8);
        foreach (float laneLine in new[] { -0.55f, 0.55f })
            for (float y = bottom; y <= top + 2f; y += 2f)
                Box(scroll, "Lane Dash", laneLine, y, 0.07f, 0.8f, new Color(0.95f, 0.88f, 0.4f), -7);

        Transform spawn = Child(transform, "Spawn Point", new Vector3(0f, -5.2f, 0f));
        Transform focus = Child(transform, "Focus Camera", Vector3.zero);
        Assign(stage, ("spawnPoint", spawn), ("cameraFocusPoint", focus), ("scrollRoot", scroll));

        // The traffic: the 30 cars of the bk_cars1.a sheet, element N = bk_cars1.a_N
        Sprite[] cars = new Sprite[30];
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(CarSheetPath))
            if (asset is Sprite sprite && int.TryParse(sprite.name.Substring(sprite.name.LastIndexOf('_') + 1), out int n) && n < cars.Length)
                cars[n] = sprite;
        SerializedObject so = new SerializedObject(stage);
        SerializedProperty list = so.FindProperty("carSprites");
        list.arraySize = cars.Length;
        for (int i = 0; i < cars.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = cars[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------- Guard dogs (downtown street) ----------------

    // Side view, like the hotel: monitos walk on the sidewalk at FloorY. From left to right:
    // a donut shop, the yard with the guard dogs behind the smart fence, and a phone repair shop.
    // Upper floors with windows above, the street below the sidewalk.
    private static void BuildDogStreet(GameObject root)
    {
        DogFenceStage stage = root.GetComponent<DogFenceStage>();
        if (stage == null) stage = root.AddComponent<DogFenceStage>();
        Transform transform = root.transform;
        const float FloorY = DogFenceStage.FloorY;
        const float streetL = -3.4f, streetR = 3.4f, yardL = -1.15f, yardR = 1.15f;
        Color white = new Color(0.97f, 0.97f, 0.95f);

        // Sky and upper floors
        Box(transform, "Sky", 0f, 3.2f, 6.8f, 2.4f, new Color(0.55f, 0.76f, 0.95f), -13);
        Tiled(transform, "Upper Floors", GameSprites.BuildingWindows(false), 0f, 1.25f, 6.8f, 2.1f, -12);
        Box(transform, "Cornice", 0f, 0.17f, 6.8f, 0.12f, new Color(0.35f, 0.3f, 0.28f), -11);

        // --- Donut shop (left) ---
        float donutX = (streetL + yardL) / 2f, donutW = yardL - streetL;
        Box(transform, "Donut Wall", donutX, -1.4f, donutW, 3.1f, new Color(0.96f, 0.86f, 0.72f), -11);
        Box(transform, "Donut Window Frame", donutX - 0.35f, -1.95f, 1.2f, 1.05f, new Color(0.55f, 0.3f, 0.2f), -10);
        Box(transform, "Donut Window", donutX - 0.35f, -1.95f, 1.08f, 0.93f, new Color(0.6f, 0.8f, 0.9f), -9);
        for (int i = 0; i < 3; i++) // donuts on the shelf
        {
            SpriteAt(transform, "Donut", GameSprites.SoftCircle(), donutX - 0.7f + i * 0.35f, -2.25f, 0.28f, new Color(1f, 0.55f, 0.75f), -8);
            SpriteAt(transform, "Donut Hole", GameSprites.SoftCircle(), donutX - 0.7f + i * 0.35f, -2.25f, 0.09f, new Color(0.6f, 0.8f, 0.9f), -7);
        }
        Box(transform, "Donut Door", donutX + 0.72f, -2.35f, 0.5f, 1.3f, new Color(0.55f, 0.3f, 0.2f), -10);
        Box(transform, "Donut Door Glass", donutX + 0.72f, -2.15f, 0.34f, 0.6f, new Color(0.6f, 0.8f, 0.9f), -9);
        Awning(transform, "Donut Awning", donutX, -0.75f, donutW, new Color(1f, 0.45f, 0.7f), white);
        Box(transform, "Donut Sign", donutX, -0.2f, donutW * 0.8f, 0.36f, new Color(0.25f, 0.08f, 0.18f), -9);
        WorldText(transform, "DONUTS", donutX, -0.2f, donutW * 0.7f, 0.28f, new Color(1f, 0.6f, 0.8f), -8);

        // --- The kennel: a small cage packed with dogs, between the two shops ---
        float yardX = (yardL + yardR) / 2f, yardW = yardR - yardL;
        const float kennelW = 1.7f, kennelH = 1.45f;
        Box(transform, "Wall Between Shops", yardX, -1.4f, yardW, 3.1f, new Color(0.62f, 0.38f, 0.32f), -11);
        for (float y = FloorY + 0.25f; y < 0.1f; y += 0.25f) // brick rows
            Box(transform, "Brick Line", yardX, y, yardW, 0.025f, new Color(0.45f, 0.26f, 0.22f), -10);
        Box(transform, "Kennel Back", yardX, FloorY + kennelH / 2f, kennelW, kennelH, new Color(0.16f, 0.15f, 0.17f), -9);
        Box(transform, "Kennel Floor", yardX, FloorY + 0.05f, kennelW, 0.1f, new Color(0.35f, 0.3f, 0.25f), -8);
        Box(transform, "Kennel Roof", yardX, FloorY + kennelH + 0.08f, kennelW + 0.25f, 0.16f, new Color(0.3f, 0.32f, 0.36f), -2);
        Box(transform, "Beware Border", yardX, -0.55f, 1.78f, 0.5f, new Color(0.8f, 0.1f, 0.1f), -10);
        Box(transform, "Beware Board", yardX, -0.55f, 1.7f, 0.42f, white, -9);
        WorldText(transform, "BEWARE OF DOGS", yardX, -0.55f, 1.55f, 0.3f, new Color(0.8f, 0.1f, 0.1f), -8);

        // Five dogs packed in: two at the back (darker, higher) and three at the front
        SpriteRenderer[] dogs = new SpriteRenderer[5];
        float[] dogX = { -0.4f, 0.35f, -0.55f, 0f, 0.55f };
        for (int i = 0; i < dogs.Length; i++)
        {
            bool back = i < 2;
            dogs[i] = SpriteAt(transform, "Guard Dog", GameSprites.Dog(0), yardX + dogX[i], FloorY + (back ? 0.62f : 0.33f),
                back ? 0.95f : 1.1f, back ? new Color(0.7f, 0.7f, 0.7f) : Color.white, back ? -6 : -4);
            dogs[i].flipX = i % 2 == 1;
        }

        // The smart gate: thin steel bars with big gaps (the dogs are fully visible), rolls up when released
        Transform gate = Child(transform, "Gate", new Vector3(yardX, FloorY, 0f));
        for (float x = -kennelW / 2f + 0.08f; x <= kennelW / 2f - 0.07f; x += 0.19f)
            Box(gate, "Bar", x, kennelH / 2f, 0.045f, kennelH, new Color(0.62f, 0.66f, 0.72f), -3);
        Box(gate, "Rail Top", 0f, kennelH - 0.05f, kennelW, 0.07f, new Color(0.25f, 0.28f, 0.33f), -2);
        Box(gate, "Rail Middle", 0f, kennelH * 0.55f, kennelW, 0.05f, new Color(0.25f, 0.28f, 0.33f), -2);
        Box(gate, "Rail Bottom", 0f, 0.05f, kennelW, 0.07f, new Color(0.25f, 0.28f, 0.33f), -2);
        SpriteRenderer led = Box(gate, "LED Strip", 0f, kennelH - 0.11f, kennelW - 0.1f, 0.035f, new Color(0.2f, 0.85f, 1f), -1);
        WorldText(gate, "SMART GATE 3000", 0f, kennelH * 0.55f + 0.1f, 1.1f, 0.12f, new Color(0.2f, 0.85f, 1f), -1);
        Box(transform, "Gate Post L", yardX - kennelW / 2f - 0.04f, FloorY + kennelH / 2f, 0.08f, kennelH, new Color(0.18f, 0.2f, 0.24f), -2);
        Box(transform, "Gate Post R", yardX + kennelW / 2f + 0.04f, FloorY + kennelH / 2f, 0.08f, kennelH, new Color(0.18f, 0.2f, 0.24f), -2);
        Box(transform, "Control Post", yardR + 0.14f, FloorY + 0.55f, 0.16f, 1.1f, new Color(0.2f, 0.2f, 0.24f), -2);
        Box(transform, "Control Screen", yardR + 0.14f, FloorY + 0.95f, 0.14f, 0.22f, new Color(0.03f, 0.04f, 0.06f), -1);
        SpriteRenderer status = SpriteAt(transform, "Status Light", GameSprites.SoftCircle(), yardR + 0.14f, FloorY + 1.2f, 0.16f, new Color(1f, 0.15f, 0.15f), 0);
        TextMeshPro screen = WorldText(transform, "LOCKED", yardR + 0.14f, FloorY + 0.95f, 0.13f, 0.06f, new Color(1f, 0.15f, 0.15f), 0);

        // Danger zone painted on the sidewalk in front of the kennel: yellow and black stripes.
        // It's exactly where the dogs bite, so the player knows when to open the gate.
        float zoneW = stage.BiteZoneWidth;
        Transform zone = Child(transform, "Danger Zone", new Vector3(yardX, FloorY - 0.25f, 0f));
        const float stripe = 0.16f;
        for (float x = -zoneW / 2f + stripe / 2f; x < zoneW / 2f; x += stripe)
        {
            bool yellow = Mathf.RoundToInt((x + zoneW / 2f) / stripe) % 2 == 0;
            Box(zone, "Stripe", x, 0f, stripe, 0.36f, yellow ? new Color(1f, 0.82f, 0.05f, 0.85f) : new Color(0.08f, 0.08f, 0.08f, 0.85f), -5);
        }
        Box(zone, "Edge L", -zoneW / 2f, 0f, 0.04f, 0.45f, new Color(1f, 0.82f, 0.05f), -5);
        Box(zone, "Edge R", zoneW / 2f, 0f, 0.04f, 0.45f, new Color(1f, 0.82f, 0.05f), -5);

        // --- Phone repair shop (right) ---
        float phoneX = (yardR + streetR) / 2f + 0.15f, phoneW = streetR - yardR - 0.3f;
        Box(transform, "Phone Wall", phoneX, -1.4f, phoneW + 0.3f, 3.1f, new Color(0.78f, 0.85f, 0.92f), -11);
        Box(transform, "Phone Window Frame", phoneX + 0.3f, -1.95f, 1.1f, 1.05f, new Color(0.2f, 0.25f, 0.35f), -10);
        Box(transform, "Phone Window", phoneX + 0.3f, -1.95f, 0.98f, 0.93f, new Color(0.6f, 0.8f, 0.9f), -9);
        for (int i = 0; i < 3; i++) // phones on display (with a lit screen)
        {
            Box(transform, "Phone", phoneX + 0.02f + i * 0.28f, -2.1f, 0.16f, 0.3f, new Color(0.08f, 0.08f, 0.1f), -8);
            Box(transform, "Phone Screen", phoneX + 0.02f + i * 0.28f, -2.09f, 0.12f, 0.22f, new Color(0.4f, 0.9f, 1f), -7);
        }
        Box(transform, "Phone Door", phoneX - 0.6f, -2.35f, 0.5f, 1.3f, new Color(0.2f, 0.25f, 0.35f), -10);
        Awning(transform, "Phone Awning", phoneX, -0.75f, phoneW + 0.3f, new Color(0.2f, 0.45f, 0.95f), white);
        Box(transform, "Phone Sign", phoneX, -0.2f, phoneW, 0.36f, new Color(0.06f, 0.1f, 0.2f), -9);
        WorldText(transform, "PHONE REPAIR", phoneX, -0.2f, phoneW * 0.9f, 0.26f, new Color(0.4f, 0.9f, 1f), -8);

        // --- Sidewalk, curb and street ---
        Tiled(transform, "Sidewalk", GameSprites.Pavement(false), 0f, FloorY - 0.25f, 6.8f, 0.5f, -6);
        Box(transform, "Curb", 0f, FloorY - 0.55f, 6.8f, 0.1f, new Color(0.55f, 0.55f, 0.55f), -6);
        Box(transform, "Street", 0f, FloorY - 4.1f, 6.8f, 7f, new Color(0.22f, 0.22f, 0.25f), -7);
        for (float x = streetL; x < streetR; x += 1.2f)
            Box(transform, "Lane Dash", x + 0.3f, FloorY - 2.2f, 0.6f, 0.07f, new Color(0.95f, 0.88f, 0.4f), -6);

        // Street furniture: hydrant and a lamp post
        Box(transform, "Hydrant", 2.75f, FloorY + 0.2f, 0.2f, 0.4f, new Color(0.85f, 0.15f, 0.12f), -5);
        Box(transform, "Hydrant Cap", 2.75f, FloorY + 0.43f, 0.26f, 0.08f, new Color(0.7f, 0.1f, 0.08f), -5);
        Box(transform, "Lamp Post", -3.0f, FloorY + 1.4f, 0.08f, 2.8f, new Color(0.15f, 0.15f, 0.18f), -5);
        Box(transform, "Lamp", -2.9f, FloorY + 2.8f, 0.3f, 0.12f, new Color(0.15f, 0.15f, 0.18f), -5);

        Transform spawn = Child(transform, "Spawn Point", new Vector3(-3.5f, FloorY + 1.0f, 0f)); // the monito pivot is at its head
        Transform focus = Child(transform, "Focus Camera", Vector3.zero);
        Assign(stage, ("gate", gate), ("statusLight", status), ("ledStrip", led), ("screenText", screen),
            ("spawnPoint", spawn), ("cameraFocusPoint", focus), ("npcPrefab", Monito));

        SerializedObject so = new SerializedObject(stage);
        so.FindProperty("npcMoveSpeed").floatValue = 1.1f; // a bit slower than the other stages: easier timing
        SerializedProperty list = so.FindProperty("dogs");
        list.arraySize = dogs.Length;
        for (int i = 0; i < dogs.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = dogs[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Striped shop awning with a scalloped (zig-zag) lower edge
    private static void Awning(Transform parent, string name, float x, float y, float width, Color stripe, Color white)
    {
        const float stripeW = 0.25f, height = 0.45f;
        int count = Mathf.CeilToInt(width / stripeW);
        float start = x - width / 2f + stripeW / 2f;
        for (int i = 0; i < count; i++)
        {
            float sx = start + i * stripeW;
            float w = Mathf.Min(stripeW, x + width / 2f - (sx - stripeW / 2f));
            Color c = i % 2 == 0 ? stripe : white;
            Box(parent, name, sx - (stripeW - w) / 2f, y, w, height, c, -9);
            Box(parent, name + " Scallop", sx - (stripeW - w) / 2f, y - height / 2f - 0.05f, w * 0.7f, 0.1f, c, -9);
        }
        Box(parent, name + " Shadow", x, y - height / 2f - 0.16f, width, 0.08f, new Color(0f, 0f, 0f, 0.18f), -10);
    }
}
