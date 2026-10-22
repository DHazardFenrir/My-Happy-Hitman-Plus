using UnityEngine;

public class PianoTrap : MonoBehaviour
{
    [SerializeField] private PianoDropStage stageManager;
    [Tooltip("Qué tan centrado debe estar el monito respecto al piano para contar como aplastado (fracción del ancho del piano)")]
    [Range(0.1f, 1f)] [SerializeField] private float hitWidth = 0.7f;

    private Collider2D pianoCollider;

    private void Awake() => pianoCollider = GetComponent<Collider2D>();

    // Tocó a un monito (collider trigger). Un roce de lado no cuenta: debe quedar debajo del piano.
    private void OnTriggerEnter2D(Collider2D collision) => CheckHit(collision);
    private void OnTriggerStay2D(Collider2D collision) => CheckHit(collision);

    private void CheckHit(Collider2D collision)
    {
        if (stageManager == null || pianoCollider == null || !stageManager.isActive) return;
        if (!collision.TryGetComponent(out NPCWalker _)) return;

        Bounds zone = pianoCollider.bounds;
        NPCWalker victim = stageManager.FindVictim(zone, zone.extents.x * hitWidth);
        if (victim != null) stageManager.OnPianoHitNPC(victim);
    }

    // Se estrelló contra el suelo sin aplastar a nadie
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.GetComponent<TrapBlocker>() != null)
        {
            if (stageManager != null) stageManager.OnTrapMissed();
        }
    }
}
