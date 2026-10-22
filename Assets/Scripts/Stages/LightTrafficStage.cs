using UnityEngine;

public class LightTrafficStage : WarioStage
{
    [SerializeField] private SpriteRenderer trafficLightSprite;
    [SerializeField] private GameObject redGlow;
    [SerializeField] private GameObject greenGlow;
    [SerializeField] private CarTrap car;

    [Header("Choque")]
    [Tooltip("Si el prefab no tiene un TrapBlocker, se crea una pared a esta distancia por encima de la cabeza de los monitos")]
    [SerializeField] private float autoWallOffset = 0.8f;

    private bool trapTriggered = false;

    public override void SetupStage(NPCAppearance.ClothingPart target)
    {
        base.SetupStage(target);

        if (redGlow != null) redGlow.SetActive(true);
        if (greenGlow != null) greenGlow.SetActive(false);

        // Pared contra la que se estrella el carro si no atropella a nadie
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
            car.Activate(this);
            BeginTrapResolve();
        }
        else
        {
            Debug.LogError("CarTrap is not assigned in the Inspector!");
            OnTrapMissed();
        }
    }

    public void OnCarHitNPC(NPCWalker npc) => OnTrapHitNPC(npc);
}
