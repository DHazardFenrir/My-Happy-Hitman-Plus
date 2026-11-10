using UnityEngine;

public class LightTrafficStage : WarioStage
{
    [SerializeField] private SpriteRenderer trafficLightSprite;
    [SerializeField] private GameObject redGlow;
    [SerializeField] private GameObject greenGlow;
    [SerializeField] private CarTrap car;

    [Header("Crash")]
    [Tooltip("If the prefab has no TrapBlocker, a wall is created this far above the monitos' heads")]
    [SerializeField] private float autoWallOffset = 0.8f;

    private bool trapTriggered = false;

    public override WalkTheme Theme => WalkTheme.Crosswalk;
    public override IrlDeathKind IrlDeath => IrlDeathKind.HitByCar;
    public override Sprite IrlThreatSprite => car != null && car.TryGetComponent(out SpriteRenderer sr) ? sr.sprite : null;

    public override void SetupStage(NPCAppearance.ClothingPart target)
    {
        base.SetupStage(target);

        if (redGlow != null) redGlow.SetActive(true);
        if (greenGlow != null) greenGlow.SetActive(false);

        // Wall the car crashes into if it doesn't hit anyone
        if (car != null && GetComponentInChildren<TrapBlocker>() == null)
        {
            Vector3 wallPos = new Vector3(car.transform.position.x, NPCHeadY() + autoWallOffset + 0.15f, 0f);
            TrapBlocker.Create(transform, "Car Wall (auto)", wallPos, new Vector2(3f, 0.3f));
        }
    }

    protected override void OnStageUpdate()
    {
        if (!trapTriggered && SingleInput.ActionPressed)
        {
            TriggerTrap();
        }
    }

    void TriggerTrap()
    {
        trapTriggered = true;

        if (redGlow != null) redGlow.SetActive(false);
        if (greenGlow != null) greenGlow.SetActive(true);

        if (car != null)
        {
            SoundFX.Play(Sfx.CarStart);
            car.Activate(this);
            BeginTrapResolve();
        }
        else
        {
            Debug.LogError("CarTrap is not assigned in the Inspector!");
            OnTrapMissed();
        }
    }

    public override bool TryGetTrapAim(out float worldX, out float delay)
    {
        worldX = delay = 0f;
        if (car == null) return false;
        Bounds b = car.Bounds;
        worldX = b.center.x;
        delay = Mathf.Max(0f, (NPCFeetY() - b.max.y) / car.speed);
        return true;
    }

    public void OnCarHitNPC(NPCWalker npc) => OnTrapHitNPC(npc);
}
