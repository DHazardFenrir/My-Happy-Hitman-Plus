using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Neon-style halo behind a TextMeshPro text (UI or world). Instead of the font shader's glow (which
// breaks the outline), it draws a few faint copies of the text around it in the glow color, plus a
// hard drop shadow. The copies follow the main text every frame (text, size, alpha, scale), so the
// text can be changed and animated normally.
[RequireComponent(typeof(TMP_Text))]
public class TextGlow : MonoBehaviour
{
    public Color glowColor = new Color(1f, 0.2f, 0.3f);
    [Tooltip("How far the halo spreads, in UI pixels (or world units for 3D text)")]
    public float radius = 6f;
    [Range(0f, 1f)] public float glowAlpha = 0.35f;
    public Vector2 shadowOffset = new Vector2(6f, -6f);
    [Range(0f, 1f)] public float shadowAlpha = 0.85f;

    private TMP_Text main;
    private readonly List<TMP_Text> layers = new List<TMP_Text>();
    private readonly List<Vector2> offsets = new List<Vector2>();
    private readonly List<float> alphas = new List<float>();
    private readonly List<Color> colors = new List<Color>();

    // Adds (or reconfigures) the glow on a text
    public static TextGlow Apply(TMP_Text text, Color glow, float radius, float glowAlpha = 0.35f, float shadowDistance = 6f)
    {
        TextGlow g = text.GetComponent<TextGlow>();
        if (g == null) g = text.gameObject.AddComponent<TextGlow>();
        g.glowColor = glow;
        g.radius = radius;
        g.glowAlpha = glowAlpha;
        g.shadowOffset = new Vector2(shadowDistance, -shadowDistance);
        g.Rebuild();
        return g;
    }

    public void SetColor(Color glow)
    {
        glowColor = glow;
        for (int i = 0; i < colors.Count; i++)
            if (i > 0) colors[i] = glow; // layer 0 is the black shadow
    }

    private void Rebuild()
    {
        main = GetComponent<TMP_Text>();
        foreach (TMP_Text layer in layers) if (layer != null) Destroy(layer.gameObject);
        layers.Clear(); offsets.Clear(); alphas.Clear(); colors.Clear();

        // Hard shadow first (furthest back), then the halo ring, wider and fainter outside
        AddLayer(shadowOffset, Color.black, shadowAlpha, 0.35f);
        const int ring = 8;
        for (int r = 0; r < 2; r++)
        {
            float dist = radius * (r == 0 ? 0.5f : 1f);
            float a = glowAlpha * (r == 0 ? 1f : 0.55f);
            for (int i = 0; i < ring; i++)
            {
                float ang = (i + r * 0.5f) / ring * Mathf.PI * 2f;
                AddLayer(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist, glowColor, a, 0.3f);
            }
        }
        // With auto-size, fontSize is only the real size after a layout pass: without this the copies
        // show up for one frame at the maximum size (a giant flash of the text)
        main.ForceMeshUpdate();
        LateUpdate();
        SetLayersActive(isActiveAndEnabled); // the text may be hidden right now
    }

    private void AddLayer(Vector2 offset, Color color, float alpha, float outline)
    {
        GameObject go = new GameObject(name + " Glow", typeof(RectTransform));
        go.transform.SetParent(transform.parent, false);
        go.transform.SetSiblingIndex(transform.GetSiblingIndex()); // behind the main text

        TMP_Text layer = main is TextMeshProUGUI ? go.AddComponent<TextMeshProUGUI>() : (TMP_Text)go.AddComponent<TextMeshPro>();
        layer.raycastTarget = false;
        layer.font = main.font;
        layer.fontStyle = main.fontStyle;
        layer.alignment = main.alignment;
        layer.enableWordWrapping = main.enableWordWrapping;
        layer.enableAutoSizing = false; // follows the main text's final size
        layer.richText = main.richText;
        layer.outlineWidth = outline;
        if (layer is TextMeshPro world && main is TextMeshPro mainWorld) world.sortingOrder = mainWorld.sortingOrder - 1;

        layers.Add(layer);
        offsets.Add(offset);
        alphas.Add(alpha);
        colors.Add(color);
    }

    void LateUpdate()
    {
        if (main == null) return;
        RectTransform src = main.rectTransform;
        bool ui = main is TextMeshProUGUI;
        bool visible = main.enabled;
        if (visible && main.enableAutoSizing && main.havePropertiesChanged) main.ForceMeshUpdate(); // same as in Rebuild

        for (int i = 0; i < layers.Count; i++)
        {
            TMP_Text layer = layers[i];
            if (layer == null) continue;
            if (layer.gameObject.activeSelf != visible) layer.gameObject.SetActive(visible);
            if (!visible) continue;

            RectTransform rt = layer.rectTransform;
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.pivot = src.pivot;
            rt.sizeDelta = src.sizeDelta;
            if (ui) rt.anchoredPosition = src.anchoredPosition + offsets[i];
            else rt.localPosition = src.localPosition + (Vector3)(offsets[i]);
            rt.localRotation = src.localRotation;
            rt.localScale = src.localScale;

            if (layer.text != main.text) layer.text = main.text;
            layer.fontSize = main.fontSize;
            layer.characterSpacing = main.characterSpacing;
            Color c = colors[i];
            c.a = alphas[i] * main.alpha;
            layer.color = c;
            layer.outlineColor = new Color(c.r, c.g, c.b, c.a);
        }
    }

    // The layers are siblings, so they must be hidden/shown by hand with the main text
    void OnEnable() => SetLayersActive(true);
    void OnDisable() => SetLayersActive(false);

    private void SetLayersActive(bool active)
    {
        foreach (TMP_Text layer in layers)
            if (layer != null) layer.gameObject.SetActive(active);
    }

    void OnDestroy()
    {
        foreach (TMP_Text layer in layers) if (layer != null) Destroy(layer.gameObject);
    }
}
