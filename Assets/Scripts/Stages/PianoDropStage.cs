using UnityEngine;

public class PianoDropStage : WarioStage
{
    [SerializeField] private Rigidbody2D pianoRigidbody;
    [SerializeField] private float dropGravityScale = 5f;
    [SerializeField] private SpriteRenderer workerSprite;
    [SerializeField] private Sprite workerSpriteChanged;
    [SerializeField] private Animator craneAnim;

    [Header("Crash")]
    [Tooltip("If the prefab has no TrapBlocker, a ground is created this far below the monitos' feet")]
    [SerializeField] private float autoGroundOffset = 0.05f;

    private bool dropped = false;

    public override WalkTheme Theme => WalkTheme.Construction;
    public override string ActionVerb => "DROP!";
    public override IrlDeathKind IrlDeath => IrlDeathKind.CrushedFromAbove;
    public override Sprite IrlThreatSprite => pianoRigidbody != null && pianoRigidbody.TryGetComponent(out SpriteRenderer sr) ? sr.sprite : null;
    private bool freezeCrane = false;

    public override void SetupStage(NPCAppearance.ClothingPart target)
    {
        base.SetupStage(target);

        if (pianoRigidbody != null)
        {
            // The piano hangs until it's dropped
            pianoRigidbody.bodyType = RigidbodyType2D.Kinematic;
            pianoRigidbody.velocity = Vector2.zero;
        }

        // Ground the piano crashes into if it doesn't crush anyone
        if (pianoRigidbody != null && GetComponentInChildren<TrapBlocker>() == null)
        {
            Vector3 groundPos = new Vector3(pianoRigidbody.position.x, NPCFeetY() - autoGroundOffset - 0.15f, 0f);
            TrapBlocker.Create(transform, "Piano Ground (auto)", groundPos, new Vector2(12f, 0.3f));
        }
    }

    protected override void OnStageUpdate()
    {
        if (!dropped && SingleInput.ActionPressed)
        {
            DropPiano();
        }
    }

    protected override void Update()
    {
        base.Update();

        // Freeze the crane at the end of its loop after the piano is dropped
        if (freezeCrane && craneAnim != null)
        {
            float progress = craneAnim.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
            if (progress >= 0.95f)
            {
                craneAnim.speed = 0f;
                freezeCrane = false;
            }
        }
    }

    void DropPiano()
    {
        dropped = true;
        freezeCrane = true;
        SoundFX.Play(Sfx.PianoWhistle);

        if (pianoRigidbody != null)
        {
            pianoRigidbody.bodyType = RigidbodyType2D.Dynamic;
            pianoRigidbody.gravityScale = dropGravityScale;
            pianoRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (workerSprite != null)
        {
            workerSprite.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            if (workerSpriteChanged != null) workerSprite.sprite = workerSpriteChanged;
        }

        BeginTrapResolve();
    }

    public override bool TryGetTrapAim(out float worldX, out float delay)
    {
        worldX = delay = 0f;
        if (pianoRigidbody == null || !pianoRigidbody.TryGetComponent(out Collider2D col)) return false;
        Bounds b = col.bounds;
        worldX = b.center.x;
        float gravity = Mathf.Abs(Physics2D.gravity.y) * dropGravityScale;
        delay = Mathf.Sqrt(2f * Mathf.Max(0f, b.min.y - NPCHeadY()) / gravity);
        return true;
    }

    public override bool TryGetTrapZone(out float centerX, out float halfWidth)
    {
        bool ok = TryGetTrapAim(out centerX, out _);
        halfWidth = ok ? pianoRigidbody.GetComponent<Collider2D>().bounds.extents.x : 0f;
        return ok;
    }

    public void OnPianoHitNPC(NPCWalker npc) => OnTrapHitNPC(npc);
}
