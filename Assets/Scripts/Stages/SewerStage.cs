using UnityEngine;

public class SewerStage : WarioStage
{
    [Header("Stage Config")]
    [SerializeField] private SewerTrap sewerTrap;

    private bool trapTriggered = false;

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

    public void OnNPCFellInSewer(NPCWalker targetNpc) => OnTrapHitNPC(targetNpc);
}
