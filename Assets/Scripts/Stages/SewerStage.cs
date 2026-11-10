using UnityEngine;

public class SewerStage : WarioStage
{
    [Header("Stage Config")]
    [SerializeField] private SewerTrap sewerTrap;

    private bool trapTriggered = false;

    public override WalkTheme Theme => WalkTheme.Sewer;
    public override IrlDeathKind IrlDeath => IrlDeathKind.Fall;

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

        if (sewerTrap != null)
        {
            sewerTrap.Activate(this);
        }
        else
        {
            Debug.LogError("SewerTrap is not assigned in the Inspector!");
            OnTrapMissed();
        }
    }

    public override bool TryGetTrapAim(out float worldX, out float delay)
    {
        worldX = delay = 0f;
        if (sewerTrap == null) return false;
        worldX = sewerTrap.Zone.center.x;
        return true;
    }

    public void OnNPCFellInSewer(NPCWalker targetNpc) => OnTrapHitNPC(targetNpc);
}
