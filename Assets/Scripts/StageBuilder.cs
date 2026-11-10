using TMPro;
using UnityEngine;

// Helpers to build things in code (colored rectangles, tiled textures and text in the world).
// Used by the drone camera HUD at runtime and by the editor's StageSetBaker.
public static class StageBuilder
{
    public static SpriteRenderer Box(Transform parent, string name, float x, float y, float w, float h, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, y, 0f);
        go.transform.localScale = new Vector3(w, h, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GameSprites.Pixel();
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    public static SpriteRenderer Tiled(Transform parent, string name, Sprite sprite, float x, float y, float w, float h, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, y, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(w, h);
        sr.sortingOrder = order;
        return sr;
    }

    public static SpriteRenderer SpriteAt(Transform parent, string name, Sprite sprite, float x, float y, float scale, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, y, 0f);
        go.transform.localScale = Vector3.one * scale;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    public static TextMeshPro WorldText(Transform parent, string text, float x, float y, float w, float h, Color color, int order = -6)
    {
        GameObject go = new GameObject(text);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, y, -0.01f);
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.05f;
        tmp.fontSizeMax = 20f;
        tmp.rectTransform.sizeDelta = new Vector2(w, h);
        tmp.sortingOrder = order; // default: on the wall, behind the monitos (3-4)
        return tmp;
    }
}
