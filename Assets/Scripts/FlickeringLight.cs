using UnityEngine;

[RequireComponent(typeof(Light))]
public class FlickeringLight : MonoBehaviour
{
    [Header("Flicker Settings")]
    public float minIntensity = 0.5f;
    public float maxIntensity = 2.0f;
    public float flickerSpeed = 5.0f;

    private Light fireLight;
    private float randomOffset;

    void Start()
    {
        fireLight = GetComponent<Light>();
        // Gives each fire a unique flicker pattern
        randomOffset = Random.Range(0f, 100f); 
    }

    void Update()
    {
        // PerlinNoise generates smooth, organic randomness
        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, randomOffset);
        fireLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
    }
}