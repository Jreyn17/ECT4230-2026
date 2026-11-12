using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;
using System;

public class PushClose : CoreFeatures
{
    [Header("Push")]
    [SerializeField]
    private Transform pushableObject;

    [SerializeField]
    private Grabbable grabbable;

    [SerializeField]
    private float snapDistance = 0.04f; //Once drawer is <= 4 centimeters, finish closing it

    private float closedZ;

    private Bounds bounds; //Size of mesh. Where is front side

    private PokeInteractor[] fingers; //This should controllers as well

    private readonly Dictionary<PokeInteractor, bool> wasInFront = new Dictionary<PokeInteractor, bool>();



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (pushableObject == null) pushableObject = transform; //autofill field

        if (grabbable == null) grabbable = pushableObject.GetComponent<Grabbable>(); // autofill field

        closedZ = pushableObject.localPosition.z; //Saves the starting point value

        bounds = pushableObject.GetComponent<MeshFilter>().sharedMesh.bounds;
        //All of this it pushableObjects own (local) space

        fingers = FindObjectsByType<PokeInteractor>(FindObjectsInactive.Include,
            FindObjectsSortMode.None);
    }

    private void LateUpdate()
    {
        if (grabbable != null && grabbable.SelectingPointsCount > 0)
        {
            //If a hand is GRABBING the object, do nothing
            wasInFront.Clear();
            return;
        }

        float frontFace = bounds.max.z;
        Vector3 pos = pushableObject.localPosition;
        bool wasOpen = pos.z > closedZ + 0.01f;

        foreach (var finger in fingers)
        {
            if (!finger.isActiveAndEnabled) continue;

            Vector3 tip = pushableObject.InverseTransformPoint(finger.Origin);
            float radius = finger.Radius / pushableObject.lossyScale.z;
            float tipBack = tip.z - radius;

            bool lineUp = tip.x > bounds.min.x && tip.x < bounds.max.x &&
                          tip.y > bounds.min.y && tip.y < bounds.max.y; //If you want z also, add another && here
                                                                        //tip.z > bounds.min.z && tip.z < bounds.max.z;

            wasInFront.TryGetValue(finger, out bool inFront);

            if (lineUp && inFront && tipBack < frontFace)
            {
                pos.z -= (frontFace - tipBack) * pushableObject.localScale.z;
                tipBack = frontFace;
            }

            wasInFront[finger] = lineUp && tipBack >= frontFace - 0.01f; // float value is just tolerance

            pos.z = Mathf.Max(pos.z, closedZ);
            if (pos.z - closedZ < snapDistance)
                pos.z = Mathf.MoveTowards(pos.z, closedZ, 0.6f * Time.deltaTime);

            pushableObject.localPosition = pos;

            if (wasOpen && pos.z <= closedZ + 0.01f) PlayOnEnd(); //trigger on close sound


        }
    }

}
