using UnityEngine;

public class PianoTrap : MonoBehaviour
{
    [SerializeField] private PianoDropStage stageManager;
    [Tooltip("How centered the monito must be under the piano to count as crushed (fraction of the piano width)")]
    [Range(0.1f, 1f)] [SerializeField] private float hitWidth = 0.7f;

    private Collider2D pianoCollider;

    private void Awake() => pianoCollider = GetComponent<Collider2D>();

    // Touched a monito (trigger collider). Grazing it doesn't count: it has to be under the piano.
    private void OnTriggerEnter2D(Collider2D collision) => CheckHit(collision);
    private void OnTriggerStay2D(Collider2D collision) => CheckHit(collision);

    private void CheckHit(Collider2D collision)
    {
        if (stageManager == null || pianoCollider == null || !stageManager.isActive) return;
        if (!collision.TryGetComponent(out NPCWalker _)) return;

        Bounds zone = pianoCollider.bounds;
        NPCWalker victim = stageManager.FindVictim(zone, zone.extents.x * hitWidth);
        if (victim == null) return;
        SoundFX.Play(Sfx.PianoCrush);
        stageManager.OnPianoHitNPC(victim);
    }

    // Crashed into the ground without crushing anyone
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.GetComponent<TrapBlocker>() != null)
        {
            if (stageManager == null || !stageManager.isActive) return;
            SoundFX.Play(Sfx.PianoCrash);
            stageManager.OnTrapMissed();
        }
    }
}
