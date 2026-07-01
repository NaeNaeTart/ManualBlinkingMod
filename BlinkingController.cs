using System;
using UnityEngine;

namespace ManualBlinkingMod
{
    public class BlinkingController : MonoBehaviour
    {
        public Body body = null!;

        // ─── OCULAR STATE ───────────────────────────────────────────────────
        public float drynessLevel = 0.0f;       // 0.0 (fresh) to 1.0 (bone dry)
        public float eyelidClosure = 0.0f;      // 0.0 (fully open) to 1.0 (fully closed)
        
        private bool isBlinking = false;         // active quick blink cycle
        private float blinkTimer = 0f;           // quick blink timer

        private const float CLOSE_SPEED = 15.0f; // Fade to dark REALLY FAST (closes eyes in ~0.06s)
        private const float OPEN_SPEED = 4.0f;   // Fade back to normal slightly slower (opens eyes in ~0.25s)

        private float warningSoundTimer = 0f;

        private void Awake()
        {
            body = GetComponent<Body>();
        }

        private void Update()
        {
            if (body == null || !body.alive)
            {
                return;
            }

            if (!Plugin.ConfigBlinkingEnabled.Value || !body.conscious)
            {
                // Reset state when unconscious or mod disabled
                drynessLevel = 0f;
                eyelidClosure = 0f;
                isBlinking = false;
                return;
            }

            // Read Blink Keys
            bool isHoldingBlink = Input.GetKey(Plugin.ConfigBlinkKey.Value);

            // Calculate environmental and physical drying multipliers
            float speed = body.rb != null ? body.rb.velocity.magnitude : 0f;
            float speedMult = 1.0f + Mathf.Min(1.5f, speed * 0.12f); // wind drying effect (up to 2.5x)
            float waterMult = body.inWater ? 3.0f : 1.0f;             // underwater irritation (3x)

            float finalDrySpeed = Plugin.ConfigBaseDrySpeed.Value * speedMult * waterMult;

            if (isHoldingBlink)
            {
                // Fade to dark REALLY FAST
                eyelidClosure = Mathf.Min(1.0f, eyelidClosure + Time.deltaTime * CLOSE_SPEED);
                // Eyes are closed: re-hydrate eyeballs rapidly based on closure amount
                drynessLevel = Mathf.Max(0.0f, drynessLevel - Time.deltaTime * 1.5f * eyelidClosure);
                
                isBlinking = false; // interrupt any active auto-blink
            }
            else if (isBlinking)
            {
                // Manual automatic blink animation sequence
                blinkTimer += Time.deltaTime;

                if (blinkTimer < 0.08f)
                {
                    // Closing REALLY FAST
                    eyelidClosure = Mathf.Min(1.0f, eyelidClosure + Time.deltaTime * CLOSE_SPEED);
                }
                else
                {
                    // Re-hydrated at apex
                    if (blinkTimer - Time.deltaTime < 0.08f)
                    {
                        drynessLevel = 0.0f;
                    }
                    // Opening slightly slower
                    eyelidClosure = Mathf.Max(0.0f, eyelidClosure - Time.deltaTime * OPEN_SPEED);
                }

                if (blinkTimer >= 0.35f || (blinkTimer > 0.08f && eyelidClosure <= 0.0f))
                {
                    isBlinking = false;
                    eyelidClosure = 0.0f;
                }
            }
            else
            {
                // Eyelids are open: fade back to open if they aren't already
                eyelidClosure = Mathf.Max(0.0f, eyelidClosure - Time.deltaTime * OPEN_SPEED);

                // Eyes are open and drying out
                drynessLevel = Mathf.Min(1.0f, drynessLevel + Time.deltaTime * finalDrySpeed);

                // Check for manual tap to blink
                if (Input.GetKeyDown(Plugin.ConfigBlinkKey.Value))
                {
                    isBlinking = true;
                    blinkTimer = 0f;
                }

                // If eyes dry out completely, trigger painful consequences instead of auto-blinking
                if (drynessLevel >= 1.0f)
                {
                    // Play a painful choking/gasping vocal sound from the body periodically to simulate stinging eye pain
                    if (Time.time - warningSoundTimer > UnityEngine.Random.Range(2.5f, 4.5f))
                    {
                        warningSoundTimer = Time.time;
                        Sound.Play("exert" + UnityEngine.Random.Range(1, 5), body.transform.position, true, true, null, 0.85f, 0.95f);
                    }
                }
            }

            // ─── STINGING EXHAUSTION & STAMINA PENALTIES ──────────────────
            if (drynessLevel > 0.70f)
            {
                // Steadily drain body stamina to simulate raw fatigue and physical strain
                float staminaDrainScale = (drynessLevel - 0.70f) / 0.30f; // 0.0 to 1.0
                body.stamina = Mathf.Max(0.0f, body.stamina - Time.deltaTime * 6.5f * staminaDrainScale);

                if (drynessLevel > 0.85f)
                {
                    // Inflict movement slowdown penalty as eye pain compromises spatial focus
                    float slowdownPenalty = (drynessLevel - 0.85f) * 1.50f; // up to 0.22s delay slow
                    body.temporarySlowdown = Mathf.Max(body.temporarySlowdown, slowdownPenalty);
                }
            }

            // ─── PHYSIOLOGICAL JITTER / DRY EYE SPASMS ─────────────────────
            float twitchSeverity = Mathf.Clamp01(drynessLevel * 5.0f);
            if (twitchSeverity > 0.01f && eyelidClosure < 0.2f)
            {
                // Twitches get more frequent and severe as the squint closes in
                float twitchThreshold = 0.01f + twitchSeverity * 0.13f; // up to 14% chance per frame
                if (PlayerCamera.main != null && UnityEngine.Random.value < twitchThreshold)
                {
                    float jitterSeverity = twitchSeverity * 0.065f; // violent spasms when completely dry
                    Vector3 cameraJitter = new Vector3(
                        UnityEngine.Random.Range(-1f, 1f) * jitterSeverity,
                        UnityEngine.Random.Range(-1f, 1f) * jitterSeverity,
                        0f
                    );
                    PlayerCamera.main.transform.position += cameraJitter;
                }
            }
        }

        // ─── CINEMATIC OnGUI SQUINT & BLACKOUT RENDERER ─────────────────────
        private void OnGUI()
        {
            if (body == null || !body.alive || !Plugin.ConfigBlinkingEnabled.Value || !body.conscious)
            {
                return;
            }

            // Store original GUI properties
            Color originalColor = GUI.color;

            // 1. Calculate Eyelid Closure Level (Blackout intensity)
            float blackoutAlpha = eyelidClosure;

            // Render absolute blackout if eyeballs are covered
            if (blackoutAlpha > 0.01f)
            {
                GUI.color = new Color(0.01f, 0.01f, 0.02f, blackoutAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            }

            // 2. Render procedural stinging red vignette when eyes are open but dry
            float drySeverity = Mathf.Clamp01(drynessLevel * 5.0f);
            if (blackoutAlpha < 0.95f && drySeverity > 0.01f)
            {
                // Add a pulsating effect to make the sting feel alive and throbbing
                float pulsate = 1.0f + 0.18f * Mathf.Sin(Time.time * 7f);
                float finalAlpha = 0.35f * drySeverity * pulsate * (1.0f - blackoutAlpha);

                // Draw full-screen reddish haze
                GUI.color = new Color(0.38f, 0.05f, 0.05f, finalAlpha * 0.4f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

                // Draw layered concentric borders for a procedural vignette
                int maxSteps = 4;
                for (int i = 0; i < maxSteps; i++)
                {
                    float stepFactor = (float)(i + 1) / maxSteps; // 0.25, 0.50, 0.75, 1.0
                    int thickness = (int)(Mathf.Min(Screen.width, Screen.height) * 0.08f * stepFactor * drySeverity);
                    
                    // Alpha gets softer as we go inward
                    float borderAlpha = finalAlpha * (1.0f - (stepFactor * 0.7f));
                    GUI.color = new Color(0.45f, 0.02f, 0.02f, borderAlpha);

                    // Top border
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, thickness), Texture2D.whiteTexture);
                    // Bottom border
                    GUI.DrawTexture(new Rect(0, Screen.height - thickness, Screen.width, thickness), Texture2D.whiteTexture);
                    // Left border
                    GUI.DrawTexture(new Rect(0, thickness, thickness, Screen.height - (thickness * 2)), Texture2D.whiteTexture);
                    // Right border
                    GUI.DrawTexture(new Rect(Screen.width - thickness, thickness, thickness, Screen.height - (thickness * 2)), Texture2D.whiteTexture);
                }
            }

            // 3. Render Cinematic Letterbox Squinting when eyes are open but dry
            float squintSeverity = Mathf.Clamp01(drynessLevel * 5.0f);
            if (blackoutAlpha < 0.99f && squintSeverity > 0.01f)
            {
                float openMultiplier = 1.0f - blackoutAlpha; // scale down squinting if eyelids are transitioning
                
                // Maximum height of top/bottom eyelids: each takes up to 24% of screen height (totaling 48% screen squeeze)
                float maxEyelidPercent = 0.24f;
                int eyelidHeight = (int)(Screen.height * maxEyelidPercent * squintSeverity * openMultiplier);

                if (eyelidHeight > 0)
                {
                    // Draw stinging/reddish irritated edges bordering the letterbox eyelids
                    Color eyelidColor = new Color(0.05f, 0.05f, 0.06f, 0.88f);
                    Color irritateColor = new Color(0.42f, 0.05f, 0.05f, 0.35f * squintSeverity * openMultiplier);

                    // A. Top Eyelid Squint
                    GUI.color = eyelidColor;
                    GUI.DrawTexture(new Rect(0, 0, Screen.width, eyelidHeight), Texture2D.whiteTexture);
                    GUI.color = irritateColor;
                    GUI.DrawTexture(new Rect(0, eyelidHeight, Screen.width, 3), Texture2D.whiteTexture); // top red margin

                    // B. Bottom Eyelid Squint
                    GUI.color = eyelidColor;
                    GUI.DrawTexture(new Rect(0, Screen.height - eyelidHeight, Screen.width, eyelidHeight), Texture2D.whiteTexture);
                    GUI.color = irritateColor;
                    GUI.DrawTexture(new Rect(0, Screen.height - eyelidHeight - 3, Screen.width, 3), Texture2D.whiteTexture); // bottom red margin
                }
            }

            // Restore GUI settings
            GUI.color = originalColor;
        }
    }
}
