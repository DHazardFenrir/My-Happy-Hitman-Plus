using UnityEngine;

// Fifth minigame: a city plaza at noon seen from a patrol drone. The crowd stares at their phones
// under a billboard that promises the drones keep them safe (the set is in its prefab).
// The wind makes the drone wobble a lot.
public class PlazaDroneStage : DroneCamStage
{
    public override WalkTheme Theme => WalkTheme.Street;
    protected override Color HudColor => new Color(1f, 1f, 1f, 0.9f);
    protected override string CamLabel => AssignedDrone.Tag;
}
