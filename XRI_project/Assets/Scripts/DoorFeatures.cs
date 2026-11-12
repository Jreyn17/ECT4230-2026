using JetBrains.Annotations;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class DoorFeatures : CoreFeatures
{

    [Header("Door Configurations")]
    [SerializeField]
    private Transform doorPivot; //Pivot control

    [SerializeField]
    private float maxAngle = 90.0f; //Current guess of maxAngle

    [SerializeField]
    private float doorSpeed = 1.0f;

    [SerializeField]
    private bool reverseAngleDirection = false;

    [SerializeField]
    private bool doorOpen = false;

    [SerializeField]
    private bool MakeKinematicOnOpen = false;

    [Header("Interations Configurations")]

    [SerializeField]
    private XRSocketInteractor socketInteractor; //Triggered by "Events"

    [SerializeField]
    private XRSimpleInteractable simpleInteractable;

    private void Start()
    {
        //When a key get close to a socket, add a listener
        //s = Select Enter or Exit

        socketInteractor?.selectEntered.AddListener((s) =>
        {
            OpenDoor();
        });

        socketInteractor?.selectExited.AddListener((s) =>
        {
            PlayOnEnd();
            socketInteractor.socketActive = featureUsage == FeatureUsage.Once ? false : true;
        });

        simpleInteractable?.selectEntered.AddListener((s) =>
        {
            OpenDoor();
        });
    }
    public void OpenDoor()
    {
        // If the door is not Open, Play OnStart sound
     
        if (!doorOpen)
        {
            PlayOnStart();
            doorOpen = true;
            StartCoroutine(ProcessMotion());
        }
    }

    private IEnumerator ProcessMotion()
    {
        //Keep looking to confirm the door is open

        while (doorOpen)
        {

            var angle = doorPivot.localEulerAngles.y < 180 ? doorPivot.localEulerAngles.y : doorPivot.localEulerAngles.y - 360;

            angle = reverseAngleDirection ? Mathf.Abs(angle) : angle;

            if(angle <= maxAngle)
            {
                doorPivot?.Rotate(Vector3.up, doorSpeed * Time.deltaTime * (reverseAngleDirection ? -1 : 1));
            }

            //When done, turn off Rigidbody
            else
            {
                doorOpen = false;
                var featureRigidbody = GetComponent<Rigidbody>();
                if (featureRigidbody != null && MakeKinematicOnOpen) featureRigidbody.isKinematic = true;
            }

            yield return null;

        }
    }
}

