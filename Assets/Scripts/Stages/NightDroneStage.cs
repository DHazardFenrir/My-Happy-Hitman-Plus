using System.Collections.Generic;
using UnityEngine;
using static StageBuilder;

// Sixth minigame: the same drone at night. The city is dark and people show up as green
// night-vision silhouettes: you can see where everyone is, but only the scope shows the real
// colors of their clothes. Less wind than by day, but you have to search for the target.
public class NightDroneStage : DroneCamStage
{
    private const float ScopeSize = 0.6f; // the scope sprites are 2 units wide

    private static readonly Color NightTint = new Color(0.30f, 0.68f, 0.36f);
    private static readonly Color PasserbyOutline = new Color(0.55f, 1f, 0.6f, 0.85f);
    private static readonly Color TargetOutline = new Color(1f, 0.7f, 0.1f, 1f);

    private Transform darkness;
    private Transform scopeRing;
    private readonly Dictionary<NPCWalker, (SpriteRenderer[] parts, Color[] original)> bodies = new Dictionary<NPCWalker, (SpriteRenderer[], Color[])>();
    // Outline copies of each body part (they follow its animation frame every frame)
    private readonly List<(SpriteRenderer part, SpriteRenderer copy)> outlines = new List<(SpriteRenderer, SpriteRenderer)>();
    private readonly List<SpriteRenderer> targetOutlines = new List<SpriteRenderer>();

    public override WalkTheme Theme => WalkTheme.Crosswalk;
    protected override Color HudColor => new Color(0.4f, 1f, 0.5f, 0.95f);
    protected override string CamLabel => "NIGHT VISION // " + AssignedDrone.Tag;
    protected override Color FilterTint => new Color(0.2f, 1f, 0.35f, 0.05f);

    // Darkness over the city (below the people) except inside the scope: a big dark sprite with a
    // hole in the middle that follows the crosshair (always larger than the whole view)
    protected override void BuildOverlay()
    {
        float holeRadius = ScopeSize; // the ring sprite is 2 units wide, so its radius is ScopeSize
        darkness = SpriteAt(transform, "Night Vision", GameSprites.ScopeVignette(), 0f, 0f,
            holeRadius / GameSprites.VignetteHoleRadius, new Color(0.01f, 0.06f, 0.03f, 0.9f), 2).transform;

        foreach (NPCWalker npc in spawnedNPCs)
        {
            if (npc == null) continue;
            SpriteRenderer[] parts = npc.GetComponentsInChildren<SpriteRenderer>(true);
            bodies[npc] = (parts, System.Array.ConvertAll(parts, p => p.color));
            AddOutline(npc, parts);
        }

        scopeRing = SpriteAt(transform, "Scope Ring", GameSprites.ScopeRing(), 0f, 0f, ScopeSize, Color.white, 20).transform;
    }

    // A flat-color copy of every body part, nudged in 4 directions and drawn behind it (inside the
    // monito's SortingGroup). Passers-by get a thin green line, the target a thicker orange one.
    private void AddOutline(NPCWalker npc, SpriteRenderer[] parts)
    {
        Shader solid = Shader.Find("Hidden/AgentTR8N1/SpriteSolidColor");
        if (solid == null) return;
        Material material = new Material(solid);

        float width = npc.isTarget ? 0.03f : 0.016f;
        Color color = npc.isTarget ? TargetOutline : PasserbyOutline;
        Vector3[] offsets = { Vector3.right, Vector3.left, Vector3.up, Vector3.down };
        foreach (SpriteRenderer part in parts)
        {
            foreach (Vector3 dir in offsets)
            {
                GameObject go = new GameObject("Outline");
                go.transform.SetParent(part.transform, false);
                go.transform.localPosition = part.transform.InverseTransformVector(dir * width);
                SpriteRenderer copy = go.AddComponent<SpriteRenderer>();
                copy.sprite = part.sprite;
                copy.sharedMaterial = material;
                copy.color = color;
                copy.sortingLayerID = part.sortingLayerID;
                copy.sortingOrder = part.sortingOrder - 20;
                outlines.Add((part, copy));
                if (npc.isTarget) targetOutlines.Add(copy);
            }
        }
    }

    protected override void OnAimMoved(Vector2 localAim)
    {
        foreach (var (part, copy) in outlines)
        {
            if (part == null) continue;
            copy.sprite = part.sprite;
            copy.flipX = part.flipX;
            copy.flipY = part.flipY;
        }
        Color pulse = TargetOutline;
        pulse.a = 0.65f + Mathf.PingPong(Time.time * 1.5f, 0.35f);
        foreach (SpriteRenderer copy in targetOutlines) if (copy != null) copy.color = pulse;

        darkness.localPosition = scopeRing.localPosition = new Vector3(localAim.x, localAim.y, 0f);

        // Green silhouette outside the scope, real colors as the scope passes over them
        Vector2 aimWorld = localAim + (Vector2)transform.position;
        float radius = ScopeSize * 0.9f;
        foreach (var pair in bodies)
        {
            if (pair.Key == null) continue;
            Rect body = pair.Key.BodyRect;
            Vector2 closest = new Vector2(Mathf.Clamp(aimWorld.x, body.xMin, body.xMax), Mathf.Clamp(aimWorld.y, body.yMin, body.yMax));
            float reveal = Mathf.Clamp01((radius - Vector2.Distance(aimWorld, closest)) / (radius * 0.35f));
            Color c = Color.Lerp(NightTint, Color.white, reveal);
            var (parts, original) = pair.Value;
            for (int i = 0; i < parts.Length; i++) parts[i].color = original[i] * c;
        }
    }
}
