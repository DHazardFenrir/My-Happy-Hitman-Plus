using UnityEngine;

public class PianoDropStage : WarioStage
{
    [SerializeField] private Rigidbody2D pianoRigidbody;
    [SerializeField] private float dropGravityScale = 5f;
    [SerializeField] private SpriteRenderer workerSprite;
    [SerializeField] private Sprite workerSpriteChanged;
    [SerializeField] private Animator craneAnim;

    [Header("Choque")]
    [Tooltip("Si el prefab no tiene un TrapBlocker, se crea un suelo a esta distancia por debajo de los pies de los monitos")]
    [SerializeField] private float autoGroundOffset = 0.05f;

    private bool dropped = false;
    private bool freezeCrane = false;

    public override void SetupStage(NPCAppearance.ClothingPart target)
    {
        base.SetupStage(target);

        if (pianoRigidbody != null)
        {
            // El piano espera colgado hasta que se suelta
            pianoRigidbody.bodyType = RigidbodyType2D.Kinematic;
            pianoRigidbody.velocity = Vector2.zero;
        }

        // Suelo contra el que se estrella el piano si no aplasta a nadie
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

        // Congela la grúa al final de su ciclo después de soltar el piano
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

    public void OnPianoHitNPC(NPCWalker npc) => OnTrapHitNPC(npc);
}
