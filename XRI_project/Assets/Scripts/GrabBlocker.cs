// =============================================================================
//  GrabBlocker.cs
//
//  Credits:  John Bowditch, Claude AI
//            GRID Lab (Game Research and Immersive Design), Ohio University
//  Date:     October 29, 2026
//
//  WHAT IT DOES
//  Keeps a grabbable object (a key, a disc, a drumstick...) from being pushed
//  through walls, tables, couches, or anything else that has a solid collider.
//
//  In VR nothing stops your real hand from moving "into" a virtual wall, so a
//  held object follows your hand right through it. This script watches the
//  held object. If it sinks into something deeper than "Allowed Depth":
//      1. the hand or controller is forced to let go,
//      2. the object is put back at the last spot where it was NOT inside anything,
//      3. gravity is turned on so it drops to the floor.
//
//  WHY IT WORKS FOR HANDS *AND* CONTROLLERS
//  The script never asks HOW the object was grabbed. It only looks at the object
//  itself, so hand tracking and controllers are treated the same way. It works
//  with the Meta Interaction SDK (HandGrabInteractable / GrabInteractable) and
//  with Unity's XR Interaction Toolkit (XRGrabInteractable).
//
//  SETUP (put this on the same object as the Rigidbody)
//  - The object needs at least one collider with "Is Trigger" UNCHECKED.
//  - The walls / furniture need colliders too (most imported rooms have them).
// =============================================================================

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GrabBlocker : MonoBehaviour
{
    [Header("What can block this object")]
    [Tooltip("Layers that count as walls/furniture. 'Everything' works for most projects.")]
    [SerializeField] private LayerMask blockingLayers = ~0;

    [Tooltip("OPTIONAL: drag your XR rig / player here so the object ignores the player's own colliders.")]
    [SerializeField] private Transform ignorePlayer;

    [Header("How strict")]
    [Tooltip("How far (meters) the object may sink into a surface before it drops. 0.01 = 1 cm.")]
    [SerializeField] private float allowedDepth = 0.01f;

    [Tooltip("Seconds to wait before the object can be grabbed again after a forced drop.")]
    [SerializeField] private float regrabDelay = 0.5f;

    // ---- Things the script finds on its own -----------------------------------
    private Rigidbody rb;                 // the object's physics body
    private Collider[] myColliders;       // this object's SOLID (non-trigger) colliders
    private Behaviour[] grabScripts;      // the SDK components that let you grab it
    private Vector3 lastSafePosition;     // last place the object was NOT inside a wall
    private Quaternion lastSafeRotation;
    private bool isDropping;              // true while we are in the middle of a forced drop
    private readonly Collider[] nearby = new Collider[16]; // reusable list for physics checks

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Helps a fast-falling object land on the floor instead of slipping through it.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // Collect only SOLID colliders. Trigger colliders can't tell us about walls.
        myColliders = System.Array.FindAll(
            GetComponentsInChildren<Collider>(), c => !c.isTrigger);

        if (myColliders.Length == 0)
            Debug.LogWarning($"GrabBlocker on '{name}': add a collider with Is Trigger UNCHECKED, " +
                             "or nothing will be blocked.", this);

        // Find every grab component, whatever SDK it comes from. Their class names all
        // end in "Interactable": HandGrabInteractable and GrabInteractable (Meta),
        // XRGrabInteractable (XRI). Turning these off later is what makes the hand let go.
        grabScripts = System.Array.FindAll(
            GetComponentsInChildren<MonoBehaviour>(),
            s => s.GetType().Name.EndsWith("Interactable"));

        RememberSafeSpot();
    }

    private void LateUpdate()
    {
        // LateUpdate runs after the SDKs have moved the object to follow the hand,
        // so we check where the object really ended up this frame.
        if (isDropping) return;

        // Both SDKs make the Rigidbody kinematic while the object is held,
        // so "kinematic" = "someone is holding it".
        bool isHeld = rb.isKinematic;

        if (isHeld && IsInsideSomething())
            StartCoroutine(ForceDrop());   // hand pushed it into a wall: drop it
        else
            RememberSafeSpot();            // all clear: save this spot as "safe"
    }

    // Returns true if any of this object's solid colliders is buried in a wall/furniture
    // deeper than allowedDepth.
    private bool IsInsideSomething()
    {
        foreach (Collider mine in myColliders)
        {
            // Step 1: quickly find colliders that are close to this one.
            Bounds box = mine.bounds;
            int count = Physics.OverlapBoxNonAlloc(box.center, box.extents, nearby,
                Quaternion.identity, blockingLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider other = nearby[i];
                if (ShouldIgnore(other)) continue;

                // Step 2: measure exactly how deep the two colliders overlap.
                bool overlapping = Physics.ComputePenetration(
                    mine, mine.transform.position, mine.transform.rotation,
                    other, other.transform.position, other.transform.rotation,
                    out _, out float depth);

                if (overlapping && depth > allowedDepth) return true;
            }
        }
        return false;
    }

    // Colliders we never want to count as a "wall".
    private bool ShouldIgnore(Collider other)
    {
        if (other.transform.IsChildOf(transform)) return true;   // part of this object
        if (other is CharacterController) return true;           // the player's body capsule
        if (ignorePlayer != null && other.transform.IsChildOf(ignorePlayer)) return true; // the XR rig
        return false;
    }

    // Forces the hand/controller to let go, moves the object out of the wall, and drops it.
    private IEnumerator ForceDrop()
    {
        isDropping = true;

        // 1. Turn the grab components off. Both SDKs release an object when its
        //    grab component is disabled, for hands and controllers alike.
        foreach (Behaviour grab in grabScripts) grab.enabled = false;

        // 2. Put the object back where it was last NOT inside anything,
        //    so it falls on the player's side of the wall.
        transform.SetPositionAndRotation(lastSafePosition, lastSafeRotation);

        // 3. Let physics take over so it falls to the floor.
        rb.isKinematic = false;
        rb.useGravity = true;
        StopMotion();

        // Some SDKs add a "throw" right after release. Wait one physics step and cancel it
        // so the object drops straight down instead of flying back into the wall.
        yield return new WaitForFixedUpdate();
        StopMotion();

        // 4. After a short pause, allow the object to be grabbed again.
        yield return new WaitForSeconds(regrabDelay);
        foreach (Behaviour grab in grabScripts) grab.enabled = true;

        isDropping = false;
    }

    private void StopMotion()
    {
        rb.linearVelocity = Vector3.zero;   // Note: This was reading as deprecated, so this is the undated documentation for Unity 6 name (older Unity: rb.velocity)
        rb.angularVelocity = Vector3.zero;
    }

    private void RememberSafeSpot()
    {
        lastSafePosition = transform.position;
        lastSafeRotation = transform.rotation;
    }
}
