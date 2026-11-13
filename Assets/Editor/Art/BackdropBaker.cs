using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Turns the static scenery of a stage prefab (dozens of colored rectangles, tiled sprites and sign
// texts made by StageSetBaker) into one PNG you can paint over in Aseprite, and replaces those pieces
// in the prefab with a single sprite. Parts the minigame moves (the gate, the dogs, the status light...)
// stay as they are: anything the stage component references is left alone.
// Scenery drawn above the moving parts (e.g. the kennel roof in front of the dogs) goes to a second
// "front" PNG. Existing PNGs are never overwritten: run it again after "Rebuild ... stage" and it
// reuses your painted ones.
// Needs the game scene open (it renders with its 2D global light). The hotel has its own menu item.
public static class BackdropBaker
{
    public const string Folder = "Assets/Art/StageBackdrops";
    private const int PixelsPerUnit = 64;
    private const int BakeLayer = 31;
    private const string UnlitSpriteMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    [MenuItem("Tools/Agent TR8N1/Bake stage backdrops to PNG (guard dogs, drones)")]
    public static void BakeAll()
    {
        // Dogs: scenery up to order -5 goes behind the dogs (-6 / -4); the kennel roof, the gate posts
        // and the control post (-2 / -1) are drawn in front of them
        Bake(StageSetBaker.DogsPath, "GuardDogs", frontAbove: -5);
        Bake(StageSetBaker.PlazaPath, "DronePlaza", frontAbove: int.MaxValue);
        Bake(StageSetBaker.NightPath, "DroneNight", frontAbove: int.MaxValue);
        AssetDatabase.SaveAssets();
    }

    // The hotel hallway: wallpaper, wainscot, door frame, signs... behind the monitos; the carpet and
    // the floor edges (order 10-11) are drawn in front of their feet. The cab, lever and needle move.
    [MenuItem("Tools/Agent TR8N1/Bake hotel (elevator) backdrop to PNG")]
    public static void BakeHotel()
    {
        Bake(StageSetBaker.ElevatorPath, "Hotel", frontAbove: 4);
        AssetDatabase.SaveAssets();
    }

    private static void Bake(string prefabPath, string name, int frontAbove)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { Debug.LogError("Missing " + prefabPath); return; }

        // A copy far away from everything, on its own layer, so the camera sees only this
        GameObject copy = Object.Instantiate(prefab, new Vector3(5000f, 5000f, 0f), Quaternion.identity);
        try
        {
            List<Renderer> back = new List<Renderer>(), front = new List<Renderer>();
            Split(copy, frontAbove, back, front);
            if (back.Count == 0 && front.Count == 0)
            {
                Debug.Log($"{name}: nothing left to bake (already baked)");
                return;
            }
            foreach (Transform t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = BakeLayer;

            Material material = FirstSpriteMaterial(back, front);
            List<(string path, string layer, Renderer[] parts)> layers = new List<(string, string, Renderer[])>();
            if (back.Count > 0) layers.Add(($"{Folder}/{name}_back.png", "Backdrop (baked)", back.ToArray()));
            if (front.Count > 0) layers.Add(($"{Folder}/{name}_front.png", "Front Scenery (baked)", front.ToArray()));

            // Render (or reuse) every PNG first; only touch the prefab if all of them worked
            List<(string layer, Sprite sprite, Vector3 localPos, int order)> results = new List<(string, Sprite, Vector3, int)>();
            foreach (var (path, layer, parts) in layers)
            {
                Bounds bounds = PixelBounds(parts);
                if (!File.Exists(path) && !RenderToPng(copy, parts, bounds, path))
                {
                    Debug.LogError($"{name}: the render came out empty, prefab not changed. Is the game scene (with its Global Light 2D) open?");
                    return;
                }
                Sprite sprite = ImportAsSprite(path);
                int order = int.MaxValue;
                foreach (Renderer r in parts) order = Mathf.Min(order, r.sortingOrder);
                results.Add((layer, sprite, copy.transform.InverseTransformPoint(bounds.center), order));
            }

            ReplaceInPrefab(prefabPath, copy, back, front, results, material);
            Debug.Log($"{name}: scenery baked to {Folder} — open it with right-click > Open in Aseprite");
        }
        finally
        {
            Object.DestroyImmediate(copy);
        }
    }

    // Static scenery = renderers with no children that are not part of anything the stage references
    private static void Split(GameObject root, int frontAbove, List<Renderer> back, List<Renderer> front)
    {
        HashSet<Transform> live = new HashSet<Transform>();
        foreach (MonoBehaviour behaviour in root.GetComponents<MonoBehaviour>())
        {
            SerializedProperty p = new SerializedObject(behaviour).GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                Transform t = p.objectReferenceValue is Component c ? c.transform
                    : p.objectReferenceValue is GameObject g ? g.transform : null;
                if (t != null && t != root.transform && t.IsChildOf(root.transform)) live.Add(t);
            }
        }

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.enabled || r.transform.childCount > 0 || IsUnder(r.transform, live)) continue;
            if (r is SpriteRenderer sr && sr.sprite == null) continue;
            (r.sortingOrder > frontAbove ? front : back).Add(r);
        }
    }

    private static bool IsUnder(Transform t, HashSet<Transform> live)
    {
        for (; t != null; t = t.parent)
            if (live.Contains(t)) return true;
        return false;
    }

    // The union of the parts, grown to whole pixels
    private static Bounds PixelBounds(Renderer[] parts)
    {
        Bounds b = parts[0].bounds;
        foreach (Renderer r in parts) b.Encapsulate(r.bounds);
        Vector2 size = new Vector2(Mathf.Ceil(b.size.x * PixelsPerUnit), Mathf.Ceil(b.size.y * PixelsPerUnit)) / PixelsPerUnit;
        return new Bounds(new Vector3(b.center.x, b.center.y, 0f), new Vector3(size.x, size.y, 1f));
    }

    // Renders only these parts with a temporary orthographic camera. Twice, over black and over white,
    // to recover the transparency (the 2D renderer doesn't keep the alpha of the target).
    private static bool RenderToPng(GameObject root, Renderer[] parts, Bounds bounds, string path)
    {
        HashSet<Renderer> wanted = new HashSet<Renderer>(parts);
        List<Renderer> hidden = new List<Renderer>();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            if (r.enabled && !wanted.Contains(r)) { r.enabled = false; hidden.Add(r); }

        // The 2D lights don't reach this camera, so lit sprites would come out black. The game's global
        // light is white at intensity 1, which looks exactly like unlit (only the copy is changed).
        Material unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterial);
        if (unlit == null) { Debug.LogError("Missing " + UnlitSpriteMaterial); return false; }
        foreach (Renderer r in parts)
            if (r is SpriteRenderer) r.sharedMaterial = unlit;

        int w = Mathf.RoundToInt(bounds.size.x * PixelsPerUnit), h = Mathf.RoundToInt(bounds.size.y * PixelsPerUnit);
        Camera cam = new GameObject("Backdrop Bake Camera").AddComponent<Camera>();
        try
        {
            cam.orthographic = true;
            cam.orthographicSize = bounds.size.y / 2f;
            cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, -50f);
            cam.cullingMask = 1 << BakeLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.aspect = (float)w / h;

            Color32[] overBlack = Render(cam, w, h, Color.black);
            Color32[] overWhite = Render(cam, w, h, Color.white);

            Color32[] result = new Color32[overBlack.Length];
            int opaque = 0, colored = 0;
            for (int i = 0; i < result.Length; i++)
            {
                Color32 k = overBlack[i], wh = overWhite[i];
                int alpha = 255 - ((wh.r - k.r) + (wh.g - k.g) + (wh.b - k.b)) / 3;
                alpha = Mathf.Clamp(alpha, 0, 255);
                if (alpha == 0) { result[i] = new Color32(0, 0, 0, 0); continue; }
                opaque++;
                if (k.r + k.g + k.b > 0) colored++;
                result[i] = new Color32(Unpremultiply(k.r, alpha), Unpremultiply(k.g, alpha), Unpremultiply(k.b, alpha), (byte)alpha);
            }
            // Lit sprites with no light come out black: if most of it is black, something went wrong
            if (opaque == 0 || colored * 5 < opaque) return false;

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(result);
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return true;
        }
        finally
        {
            Object.DestroyImmediate(cam.gameObject);
            foreach (Renderer r in hidden) r.enabled = true;
        }
    }

    private static byte Unpremultiply(byte c, int alpha) => (byte)Mathf.Clamp(c * 255 / alpha, 0, 255);

    private static Color32[] Render(Camera cam, int w, int h, Color background)
    {
        RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.filterMode = FilterMode.Point;
        cam.backgroundColor = background;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D read = new Texture2D(w, h, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        read.Apply();
        RenderTexture.active = previous;

        Color32[] pixels = read.GetPixels32();
        cam.targetTexture = null;
        Object.DestroyImmediate(read);
        rt.Release();
        Object.DestroyImmediate(rt);
        return pixels;
    }

    private static Sprite ImportAsSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Material FirstSpriteMaterial(List<Renderer> back, List<Renderer> front)
    {
        foreach (Renderer r in back) if (r is SpriteRenderer) return r.sharedMaterial;
        foreach (Renderer r in front) if (r is SpriteRenderer) return r.sharedMaterial;
        return null;
    }

    // Opens the prefab, deletes the baked pieces (found by their position in the hierarchy, the copy has
    // the same one), removes groups left empty, and adds the baked sprites
    private static void ReplaceInPrefab(string prefabPath, GameObject copy, List<Renderer> back, List<Renderer> front,
        List<(string layer, Sprite sprite, Vector3 localPos, int order)> results, Material material)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            List<GameObject> doomed = new List<GameObject>();
            foreach (Renderer r in back) doomed.Add(Resolve(contents.transform, PathOf(copy.transform, r.transform)).gameObject);
            foreach (Renderer r in front) doomed.Add(Resolve(contents.transform, PathOf(copy.transform, r.transform)).gameObject);

            HashSet<Transform> live = new HashSet<Transform>();
            foreach (MonoBehaviour behaviour in contents.GetComponents<MonoBehaviour>())
            {
                SerializedProperty p = new SerializedObject(behaviour).GetIterator();
                while (p.Next(true))
                    if (p.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (p.objectReferenceValue is Component c) live.Add(c.transform);
                        else if (p.objectReferenceValue is GameObject g) live.Add(g.transform);
                    }
            }

            HashSet<Transform> parents = new HashSet<Transform>();
            foreach (GameObject go in doomed)
            {
                if (go.transform.parent != contents.transform) parents.Add(go.transform.parent);
                Object.DestroyImmediate(go);
            }
            foreach (Transform parent in parents)
                if (parent != null && parent.childCount == 0 && !live.Contains(parent) && parent.GetComponents<Component>().Length == 1)
                    Object.DestroyImmediate(parent.gameObject);

            foreach (var (layer, sprite, localPos, order) in results)
            {
                GameObject go = new GameObject(layer);
                go.transform.SetParent(contents.transform, false);
                go.transform.localPosition = localPos;
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                if (material != null) sr.sharedMaterial = material;
                sr.sprite = sprite;
                sr.sortingOrder = order;
                go.transform.SetSiblingIndex(0);
            }
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static List<int> PathOf(Transform root, Transform t)
    {
        List<int> path = new List<int>();
        for (; t != root; t = t.parent) path.Insert(0, t.GetSiblingIndex());
        return path;
    }

    private static Transform Resolve(Transform root, List<int> path)
    {
        Transform t = root;
        foreach (int i in path) t = t.GetChild(i);
        return t;
    }
}
