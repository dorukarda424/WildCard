using UnityEngine;

namespace Unity.FPS.Gameplay
{
    [RequireComponent(typeof(ParticleSystem))]
    public class PortalVfxController : MonoBehaviour
    {
        private ParticleSystem m_ParticleSystem;
        private float m_BaseEmissionRate = 500f; // Extremely dense
        private float m_BaseSpeed = -10f; // Fast inward pull
        private float m_TotalAbsorbedDamage = 0f;

        private void Awake()
        {
            m_ParticleSystem = GetComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = GetComponent<ParticleSystemRenderer>();
            
            // 1. Generate a perfect Round Circle texture in code
            Texture2D circleTex = new Texture2D(32, 32);
            for (int y = 0; y < 32; y++) {
                for (int x = 0; x < 32; x++) {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                    float alpha = Mathf.Clamp01(1.0f - (dist / 16.0f));
                    alpha = Mathf.Pow(alpha, 2.0f); // Softer edges
                    circleTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            circleTex.Apply();

            // 2. Setup the Material with the circle
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.velocityScale = 0; // CRITICAL: Stop stretching!
                renderer.lengthScale = 0;   // CRITICAL: Stop stretching!
                
                Shader shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
                Material mat = new Material(shader);
                mat.mainTexture = circleTex;
                renderer.sharedMaterial = mat;
            }
            
            var main = m_ParticleSystem.main;
            main.startColor = Color.white; 
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f); // Small glowing dots
            main.startSpeed = new ParticleSystem.MinMaxCurve(m_BaseSpeed - 5f, m_BaseSpeed + 2f); 
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            
            var shape = m_ParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 2.5f; 
            shape.radiusThickness = 0.3f; 
            
            var emission = m_ParticleSystem.emission;
            emission.rateOverTime = m_BaseEmissionRate;
            
            var velocityOverLifetime = m_ParticleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.orbitalZ = new ParticleSystem.MinMaxCurve(8f); // Fast swirl
            
            var sizeOverLifetime = m_ParticleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)));

            var noise = m_ParticleSystem.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 1.0f;
            
            var colorOverLifetime = m_ParticleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.cyan, 0.0f), new GradientColorKey(Color.blue, 0.6f), new GradientColorKey(Color.black, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 0.8f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);
        }

        public void IntensifyEffect(float damageAmount)
        {
            m_TotalAbsorbedDamage += damageAmount;
            float intensifyFactor = 1f + (m_TotalAbsorbedDamage * 0.01f);
            var emission = m_ParticleSystem.emission;
            emission.rateOverTime = Mathf.Min(m_BaseEmissionRate * intensifyFactor, 1500f);
            var main = m_ParticleSystem.main;
            main.startSpeed = m_BaseSpeed * Mathf.Clamp(intensifyFactor, 1f, 4f); 
        }
        
        public void PlayAbsorptionFlash()
        {
            m_ParticleSystem.Emit(80);
        }
    }
}
