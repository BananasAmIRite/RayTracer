using UnityEngine;

public class ObjectSettings : MonoBehaviour
{
    public Color emissionColor;
    public Color materialColor;
    public float emission;
    [Range(0f, 1f)]
    public float roughness = 0.5f; // 0 = perfect mirror, 1 = perfect diffuse
}