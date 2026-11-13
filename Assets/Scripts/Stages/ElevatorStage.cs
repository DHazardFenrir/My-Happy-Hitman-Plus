using System.Collections;
using UnityEngine;

// Fourth minigame: a hotel hallway with an old cage elevator and a brass lever.
// Monitos walk down the hallway right through the cab (which sits level with the floor).
// Pull the lever while the target is inside: the elevator shoots up and the monito falls down the shaft.
// The set lives in its prefab (---- FOURTH MINI GAME (elevator) ----).
public class ElevatorStage : WarioStage
{
    [Header("Elevator")]
    [SerializeField] private float cabRiseSpeed = 14f;
    [SerializeField] private float shaftCenterX = 1.1f;
    [SerializeField] private float shaftWidth = 1.0f;

    [Header("Set (parts that move)")]
    [SerializeField] private Transform cab;
    [SerializeField] private Transform lever;
    [SerializeField] private Transform needle;

    // Hallway floor and cab height in the prefab (local units)
    private const float FloorY = -3.0f;
    private const float CabHeight = 1.8f;

    private bool triggered;

    public override WalkTheme Theme => WalkTheme.Hotel;
    public override string ActionVerb => "PULL!";
    public override IrlDeathKind IrlDeath => IrlDeathKind.Fall;

    protected override void OnStageUpdate()
    {
        if (!triggered && SingleInput.ActionPressed)
        {
            triggered = true;
            StartCoroutine(PullLever());
        }
    }

    private IEnumerator PullLever()
    {
        // Who is inside the cab right now?
        Vector3 shaft = transform.TransformPoint(new Vector3(shaftCenterX, FloorY + CabHeight / 2f, 0f));
        Bounds zone = new Bounds(shaft, new Vector3(shaftWidth, CabHeight, 1f));
        NPCWalker victim = FindVictim(zone, shaftWidth / 2f * 0.8f);
        if (victim != null) victim.CanMove = false;
        SoundFX.Play(Sfx.ElevatorLever);

        // Lever up, needle to the top floor, and the elevator shoots up
        float t = 0f;
        float startY = cab.localPosition.y;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 0.15f);
            lever.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(150f, 30f, k));
            needle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(70f, -70f, Mathf.Clamp01(t / 0.6f)));
            cab.localPosition = new Vector3(cab.localPosition.x, startY + cabRiseSpeed * t * t, 0f);
            yield return null;
        }

        if (victim == null)
        {
            OnTrapMissed();
            yield break;
        }

        // Falls down the elevator shaft
        SoundFX.Play(Sfx.ElevatorFall);
        float velocity = 0f;
        Vector3 p = victim.transform.position;
        float bottom = transform.TransformPoint(new Vector3(0f, FloorY - 4.5f, 0f)).y;
        while (p.y > bottom)
        {
            velocity += 18f * Time.deltaTime;
            p.y -= velocity * Time.deltaTime;
            victim.transform.position = p;
            yield return null;
        }
        OnTrapHitNPC(victim);
    }

    public override bool TryGetTrapAim(out float worldX, out float delay)
    {
        worldX = transform.TransformPoint(new Vector3(shaftCenterX, 0f, 0f)).x;
        delay = 0f;
        return true;
    }

    public override bool TryGetTrapZone(out float centerX, out float halfWidth)
    {
        halfWidth = shaftWidth / 2f * 0.8f; // the same tolerance PullLever uses
        return TryGetTrapAim(out centerX, out _);
    }
}
