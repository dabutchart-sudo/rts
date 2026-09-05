using UnityEngine;

/// <summary>
/// Marker component for the map-independent gameplay package.
/// Battlefield scenes provide terrain, sectors, objectives, bases and navigation;
/// this root provides the shared managers, UI, camera, input and runtime services.
/// </summary>
public sealed class SharedGameplaySystemsRoot : MonoBehaviour
{
    public const string RootObjectName = "SHARED GAMEPLAY SYSTEMS";
}
