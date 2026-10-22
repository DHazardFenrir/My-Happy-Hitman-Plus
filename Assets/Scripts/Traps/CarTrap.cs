using UnityEngine;

public class CarTrap : MonoBehaviour
{
    public float speed = 12f;
    [Tooltip("Qué tan centrado debe estar el monito respecto al carro para contar como atropellado (fracción del ancho del carro)")]
    [Range(0.1f, 1f)] public float hitWidth = 0.8f;
    private bool isDriving = false;
    private LightTrafficStage stageManager;
    private Rigidbody2D rb;
    private Collider2D carCollider;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        carCollider = GetComponent<Collider2D>();
        if (rb != null) rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    public void Activate(LightTrafficStage manager)
    {
        stageManager = manager;
        isDriving = true;
    }

    void FixedUpdate()
    {
        if (isDriving)
        {
            Vector2 nextPosition = rb.position + Vector2.up * speed * Time.fixedDeltaTime;
            rb.MovePosition(nextPosition);
        }
    }

    // Tocó a un monito (collider trigger). Se revisa en Enter y en Stay porque un roce de lado
    // no cuenta: solo cuando el monito queda en el centro del carro.
    private void OnTriggerEnter2D(Collider2D collision) => CheckHit(collision);
    private void OnTriggerStay2D(Collider2D collision) => CheckHit(collision);

    private void CheckHit(Collider2D collision)
    {
        if (!isDriving || stageManager == null || carCollider == null) return;
        if (!collision.TryGetComponent(out NPCWalker _)) return;

        Bounds zone = carCollider.bounds;
        NPCWalker victim = stageManager.FindVictim(zone, zone.extents.x * hitWidth);
        if (victim == null) return;

        Stop();
        stageManager.OnCarHitNPC(victim);
    }

    // Se estrelló contra una pared sin atropellar a nadie
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isDriving) return;

        if (collision.collider.GetComponent<TrapBlocker>() != null)
        {
            Stop();
            if (stageManager != null) stageManager.OnTrapMissed();
        }
    }

    private void Stop()
    {
        isDriving = false;
        rb.velocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }
}
