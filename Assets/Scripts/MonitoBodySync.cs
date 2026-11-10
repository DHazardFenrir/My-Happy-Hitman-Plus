using System.Collections.Generic;
using UnityEngine;

// Keeps the head on the body while the monito walks. The walk cycle bobs the body up and down (every
// sheet in Modular_Monito has the same 6 frames, 32x64 each); the arms, the pants and every shirt
// have walk animations, but the head has none and would stay on frame 0, leaving a gap at the neck.
// Every frame this puts the head (and any other part without an Animator) on the same frame as the
// arms (the arms are animated on every monito).
// Added by NPCAppearance; it needs nothing set up in the prefab.
public class MonitoBodySync : MonoBehaviour
{
    private SpriteRenderer clock;
    private readonly List<(SpriteRenderer renderer, Sprite[] frames)> parts = new List<(SpriteRenderer, Sprite[])>();

    // The frames of each sheet, made once (from the texture of the sprite the prefab uses)
    private static readonly Dictionary<Sprite, Sprite[]> framesOf = new Dictionary<Sprite, Sprite[]>();

    void Awake()
    {
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null) continue;
            if (sr.name == "Arms") { clock = sr; continue; }
            // Parts with their own walk animation (pants, red shirt) are already in step
            if (sr.GetComponent<Animator>() != null) continue;
            Sprite[] frames = Frames(sr.sprite);
            if (frames != null) parts.Add((sr, frames));
        }
    }

    void LateUpdate()
    {
        if (clock == null || clock.sprite == null) return;
        Rect cell = clock.sprite.rect;
        int frame = Mathf.RoundToInt(cell.x / cell.width);
        foreach (var (renderer, frames) in parts)
            if (renderer.gameObject.activeInHierarchy)
                renderer.sprite = frames[frame % frames.Length];
    }

    // All the cells in the same row of the sheet as this sprite (null if the sheet has a single cell)
    private static Sprite[] Frames(Sprite sprite)
    {
        if (framesOf.TryGetValue(sprite, out Sprite[] cached)) return cached;
        Texture2D tex = sprite.texture;
        Rect rect = sprite.rect;
        int count = Mathf.FloorToInt(tex.width / rect.width);
        Sprite[] frames = null;
        if (count > 1)
        {
            frames = new Sprite[count];
            Vector2 pivot = new Vector2(sprite.pivot.x / rect.width, sprite.pivot.y / rect.height);
            for (int i = 0; i < count; i++)
            {
                Rect r = new Rect(i * rect.width, rect.y, rect.width, rect.height);
                if (Mathf.Approximately(r.x, rect.x)) { frames[i] = sprite; continue; }
                frames[i] = Sprite.Create(tex, r, pivot, sprite.pixelsPerUnit, 0, SpriteMeshType.Tight);
                frames[i].name = sprite.name + "_walk" + i;
            }
        }
        framesOf[sprite] = frames;
        return frames;
    }
}
