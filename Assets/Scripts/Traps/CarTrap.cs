using UnityEngine;

public class CarTrap : MonoBehaviour
{
    public float speed = 12f;
    [Tooltip("How centered the monito must be in front of the car to count as hit (fraction of the car width)")]
    [Range(0.1f, 1f)] public float hitWidth = 0.8f;
    private bool isDriving = false;
    private LightTrafficStage stageManager;
    private Rigidbody2D rb;
    private Collider2D carCollider;

    public Bounds Bounds => carCollider != null ? carCollider.bounds : new Bounds(transform.position, Vector3.one);
    // Half of the part of the car's width that counts as a hit
    public float HitHalfWidth => Bounds.extents.x * hitWidth;

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

    // Touched a monito (trigger collider). Checked on Enter and Stay because grazing it from
    // the side doesn't count: only when the monito ends up in the middle of the car.
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
        SoundFX.Play(Sfx.CarHit);
        stageManager.OnCarHitNPC(victim);
    }

    // Crashed into a wall without hitting anyone
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isDriving) return;

        if (collision.collider.GetComponent<TrapBlocker>() != null)
        {
            Stop();
            SoundFX.Play(Sfx.CarCrash);
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
