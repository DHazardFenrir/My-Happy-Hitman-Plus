using System.Collections;
using UnityEngine;

public class NPCWalker : MonoBehaviour
{
    public float moveSpeed = 2f;

    public NPCAppearance.ClothingPart clothingPart;
    public bool isTarget = false;

    [Tooltip("Makes random stops while walking")]
    public bool randomStops = true;

    [Tooltip("Bleeds when killed")]
    public bool bleedOnDeath = true;

    // While false the monito stands still (e.g. while the card is showing)
    public bool CanMove { get; set; } = true;
    public bool IsDead { get; private set; }
    // Already left the screen (without dying)
    public bool IsGone { get; private set; }
    public bool IsPopping { get; private set; }
    public bool IsWalking => !IsDead && !IsGone && CanMove && !isStopped;
    public Vector3 WalkDirection { get; private set; } = Vector3.right;

    private bool isStopped = false;
    private Vector3 baseScale;
    private float facing = 1f;

    // Visible body (head to feet) in the monito's local space, unscaled
    private Vector2 localBodyCenter;
    private Vector2 localBodySize = new Vector2(0.15f, 0.5f);

    void Awake()
    {
        baseScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        FitBodyToSprites();
    }

    // The prefab collider only covered the head; fit it to the whole body (head to feet),
    // a bit narrower than the drawing so only "real" hits count.
    private void FitBodyToSprites()
    {
        Matrix4x4 toLocal = transform.worldToLocalMatrix;
        bool any = false;
        Vector2 min = Vector2.zero, max = Vector2.zero;

        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null) continue;
            Matrix4x4 m = toLocal * sr.transform.localToWorldMatrix;
            // The part sprites are whole 32x64 animation cells (mostly transparent): measure the drawn
            // pixels only, through the sprite's tight mesh, not its rectangle
            foreach (Vector2 v in sr.sprite.vertices)
            {
                Vector2 c = m.MultiplyPoint3x4(v);
                if (!any) { min = max = c; any = true; }
                min = Vector2.Min(min, c);
                max = Vector2.Max(max, c);
            }
        }
        if (!any) return;

        localBodyCenter = (min + max) / 2f;
        localBodySize = new Vector2((max.x - min.x) * 0.8f, max.y - min.y);

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.offset = localBodyCenter;
            box.size = localBodySize;
        }
    }

    // Body rectangle in world space (independent of the pop in/out animation)
    public Rect BodyRect
    {
        get
        {
            Vector2 size = Vector2.Scale(localBodySize, baseScale);
            Vector2 center = (Vector2)transform.position + new Vector2(localBodyCenter.x * baseScale.x * facing, localBodyCenter.y * baseScale.y);
            return new Rect(center - size / 2f, size);
        }
    }

    public Vector3 BodyCenter => BodyRect.center;

    void Start()
    {
        StartCoroutine(RandomStopRoutine());
    }

    void Update()
    {
        if (!IsDead && !IsGone && CanMove && !isStopped && !BlockedByOneAhead())
        {
            transform.Translate(WalkDirection * moveSpeed * Time.deltaTime, Space.World);
        }
    }

    // ---- Personal space ----
    // Walkers don't overlap: if the one in front (same row, same direction or standing still) is too
    // close, this one waits behind it, like a queue. People walking the other way pass each other.

    private static readonly System.Collections.Generic.List<NPCWalker> all = new System.Collections.Generic.List<NPCWalker>();

    [Tooltip("Minimum distance to the monito in front before this one waits (world units)")]
    public float personalSpace = 0.42f;
    private const float SameRowTolerance = 0.12f;

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    private bool BlockedByOneAhead()
    {
        Vector3 me = transform.position;
        foreach (NPCWalker other in all)
        {
            if (other == this || other.IsDead || other.IsGone || other.transform.parent != transform.parent) continue;
            Vector3 d = other.transform.position - me;
            if (Mathf.Abs(d.y) > SameRowTolerance) continue;
            float ahead = Vector3.Dot(d, WalkDirection);
            if (ahead <= 0f || ahead >= personalSpace) continue;
            bool sameWay = Vector3.Dot(other.WalkDirection, WalkDirection) > 0f || !other.IsWalking;
            if (sameWay) return true;
        }
        return false;
    }

    // Makes the monito bigger or smaller (e.g. a close-up camera shot); BodyRect follows
    public void ScaleBy(float factor)
    {
        baseScale *= factor;
        ApplyScale(1f);
    }

    public void SetWalkDirection(Vector3 direction)
    {
        WalkDirection = direction.normalized;

        // Flip the sprite when walking left
        facing = WalkDirection.x < 0 ? -1f : 1f;
        ApplyScale(1f);
    }

    // Pops in, growing in place
    public void PopIn(float duration = 0.25f)
    {
        IsGone = false;
        gameObject.SetActive(true);
        StartCoroutine(PopRoutine(0f, 1f, duration, false));
    }

    // Pops out, shrinking. If deactivate is true, the object is disabled at the end.
    public void PopOut(bool deactivate, float duration = 0.2f)
    {
        if (IsDead || IsPopping) return;
        StartCoroutine(PopRoutine(1f, 0f, duration, deactivate));
    }

    private IEnumerator PopRoutine(float from, float to, float duration, bool deactivateAtEnd)
    {
        IsPopping = true;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            // easeOutBack when popping in, linear when popping out
            float eased = to > from ? 1f + 2.70158f * Mathf.Pow(k - 1f, 3) + 1.70158f * Mathf.Pow(k - 1f, 2) : k;
            ApplyScale(Mathf.LerpUnclamped(from, to, eased));
            yield return null;
        }
        ApplyScale(to);
        IsPopping = false;

        if (deactivateAtEnd)
        {
            IsGone = true;
            gameObject.SetActive(false);
        }
    }

    private void ApplyScale(float amount)
    {
        transform.localScale = new Vector3(baseScale.x * facing * amount, baseScale.y * amount, baseScale.z);
    }

    IEnumerator RandomStopRoutine()
    {
        while (!IsDead)
        {
            yield return new WaitForSeconds(Random.Range(1.0f, 2.5f));
            if (!randomStops) continue;
            isStopped = true;
            yield return new WaitForSeconds(Random.Range(0.3f, 0.8f));
            isStopped = false;
        }
    }

    public void Kill()
    {
        if (IsDead) return;
        IsDead = true;
        IsPopping = false;
        SoundFX.Play(Sfx.Scream);
        StopAllCoroutines();

        if (bleedOnDeath)
        {
            Rect body = BodyRect;
            BloodSplatter.Spawn(new Vector3(body.center.x, body.center.y, transform.position.z), transform.parent, body.yMin);
        }

        gameObject.SetActive(false);
    }
}
