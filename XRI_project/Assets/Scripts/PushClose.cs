using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.PoseDetection;

public class PushClose : CoreFeatures
{
    [Header("Push")]
    [SerializeField] private Transform pushableObject;

    [SerializeField] private Grabbable grabbable;

    [SerializeField] private float snapDistance = 0.04f; //Once drawer is <= 4 centimeters, finish closing it

    private float closedZ;

    private Bounds bounds;

    private PokeInteractor[] fingers;

    private readonly Dictionary<PokeInteractor, bool> wasInFront = new Dictionary<PokeInteractor, bool>();
}
