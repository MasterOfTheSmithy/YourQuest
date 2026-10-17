using UnityEngine;

// note: Pure tuning rules are shared by the canonical motor and focused regression checks.
public static class YQPlayerMotionRules
{
    public static float BrakeSpeed(float speed, float constantBrake, float proportionalBrake, float dt)
    {
        speed = Mathf.Max(0f, speed);
        dt = Mathf.Max(0f, dt);
        float a = Mathf.Max(0f, constantBrake);
        float b = Mathf.Max(0f, proportionalBrake);
        return b < .0001f ? Mathf.Max(0f, speed - a * dt) :
            Mathf.Max(0f, (speed + a / b) * Mathf.Exp(-b * dt) - a / b);
    }

    public static float FallDamage(float height, float impactSpeed, float safeHeight, float fatalHeight,
        float safeSpeed, float fatalSpeed, float maxHealth)
    {
        // note: Contact speed excludes moving support velocity; height alone cannot make a gently caught fall lethal.
        float severity = Mathf.InverseLerp(safeSpeed, Mathf.Max(safeSpeed + .1f, fatalSpeed), impactSpeed);
        if (severity <= 0f || height <= safeHeight) return 0f;
        float heightSeverity = Mathf.InverseLerp(safeHeight, Mathf.Max(safeHeight + .1f, fatalHeight), height);
        if (severity >= 1f || heightSeverity >= 1f) return Mathf.Max(1f, maxHealth) * 2f;
        return Mathf.Max(1f, maxHealth) * Mathf.Max(severity * severity, heightSeverity * heightSeverity);
    }

    public static float ClutchHealth(float beforeImpact, float maxHealth, float acrobatics01)
    {
        // note: The clutch only mitigates damage: low skill keeps one HP, high skill retains at most 65% maximum HP.
        return Mathf.Min(beforeImpact, Mathf.Max(1f, maxHealth * .65f * Mathf.Clamp01(acrobatics01)));
    }

    public static bool InClutchWindow(float landingTime, float pressTime, float currentTime, float window)
    {
        float allowed = Mathf.Clamp(window, .3f, .5f);
        return currentTime >= landingTime && currentTime >= pressTime && currentTime - landingTime <= allowed + .00001f && Mathf.Abs(pressTime - landingTime) <= allowed + .00001f;
    }

    public static float ClutchDamage(float ordinaryDamage, float health, float beforeImpact, float maxHealth, float acrobatics01)
    {
        // note: A successful roll can only reduce the original injury, including a nonfatal landing.
        float retained = ClutchHealth(Mathf.Min(beforeImpact, health), maxHealth, acrobatics01);
        return Mathf.Min(Mathf.Max(0f, ordinaryDamage), Mathf.Max(0f, health - retained));
    }
}
