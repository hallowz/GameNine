using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Generates procedural audio clips as real .wav files on disk so they survive
/// domain reloads, then assigns them to every empty AudioClip field in the scene.
/// Run via: Tools > Generate Scene Audio Clips
/// </summary>
public static class AudioClipGenerator
{
    private const int SR = 44100;
    private const string OutRel = "Audio/Generated";

    // ── Entry point ───────────────────────────────────────────────────────────

    [MenuItem("Tools/Generate Scene Audio Clips")]
    public static void GenerateAll()
    {
        string absDir = Path.Combine(Application.dataPath, OutRel);
        Directory.CreateDirectory(absDir);

        // ---- Player combat ----
        Wav(absDir, "swing_whoosh",  Whoosh  (freq: 600f,  dur: 0.18f, amp: 0.55f));
        Wav(absDir, "hit_impact",    Impact  (freq: 90f,   dur: 0.22f, amp: 0.70f));
        Wav(absDir, "parry_success", Clang   (freq: 1100f, dur: 0.35f, amp: 0.65f));
        Wav(absDir, "parry_fail",    Thump   (freq: 80f,   dur: 0.18f, amp: 0.60f));
        Wav(absDir, "kick_swing",    Whoosh  (freq: 350f,  dur: 0.22f, amp: 0.60f));
        Wav(absDir, "kick_hit",      Impact  (freq: 60f,   dur: 0.28f, amp: 0.80f));
        Wav(absDir, "feint_swoosh",  Whoosh  (freq: 800f,  dur: 0.12f, amp: 0.40f));
        Wav(absDir, "chamber_click", Click   (dur: 0.06f,  amp: 0.70f));

        // ---- Enemy: Patrol ----
        Wav(absDir, "patrol_hit",    HurtBurst (freq: 400f, dur: 0.15f, amp: 0.55f));
        Wav(absDir, "patrol_death",  DeathFade (freq: 350f, dur: 0.55f, amp: 0.60f));
        Wav(absDir, "patrol_alert",  AlertChirp(freq: 500f, dur: 0.20f, amp: 0.55f));

        // ---- Enemy: Heavy ----
        Wav(absDir, "heavy_hit",     HurtBurst (freq: 150f, dur: 0.25f, amp: 0.75f));
        Wav(absDir, "heavy_death",   DeathFade (freq: 100f, dur: 0.80f, amp: 0.80f));
        Wav(absDir, "heavy_alert",   AlertChirp(freq: 200f, dur: 0.30f, amp: 0.70f));

        // ---- Enemy: Ranged ----
        Wav(absDir, "ranged_hit",    HurtBurst (freq: 350f, dur: 0.15f, amp: 0.50f));
        Wav(absDir, "ranged_death",  DeathFade (freq: 300f, dur: 0.50f, amp: 0.55f));
        Wav(absDir, "ranged_alert",  AlertChirp(freq: 450f, dur: 0.18f, amp: 0.50f));

        // ---- Enemy: Sentinel ----
        Wav(absDir, "sentinel_hit",   ElecGlitch  (dur: 0.14f, amp: 0.60f));
        Wav(absDir, "sentinel_death", ElecShutdown(dur: 0.60f, amp: 0.65f));
        Wav(absDir, "sentinel_alert", ElecAlert   (dur: 0.22f, amp: 0.65f));

        // ---- Enemy: Crafter ----
        Wav(absDir, "crafter_hit",   Impact    (freq: 200f, dur: 0.18f, amp: 0.65f));
        Wav(absDir, "crafter_death", CrashNoise(dur: 0.70f, amp: 0.70f));
        Wav(absDir, "crafter_alert", AlertChirp(freq: 300f, dur: 0.25f, amp: 0.60f));

        // ---- Enemy: Stalker ----
        Wav(absDir, "stalker_hit",   HissHurt  (dur: 0.18f, amp: 0.60f));
        Wav(absDir, "stalker_death", HissShriek(dur: 0.65f, amp: 0.70f));
        Wav(absDir, "stalker_alert", HissGrowl (dur: 0.28f, amp: 0.60f));

        // ---- Ranged enemy fire sound ----
        Wav(absDir, "ranged_fire",    RangedShot(dur: 0.20f, amp: 0.75f));

        // ---- Melee attack swings (per enemy type) ----
        Wav(absDir, "patrol_attack",   LightSwing (dur: 0.18f, amp: 0.60f));
        Wav(absDir, "heavy_attack",    HeavySwing (dur: 0.25f, amp: 0.80f));
        Wav(absDir, "sentinel_attack", ElecStrike (dur: 0.18f, amp: 0.65f));
        Wav(absDir, "crafter_attack",  MechSlam   (dur: 0.22f, amp: 0.70f));
        Wav(absDir, "stalker_attack",  StalkerLunge(dur: 0.20f, amp: 0.65f));

        // ---- Ambient (AudioSource.clip) ----
        Wav(absDir, "patrol_ambient",   SineWave(freq: 180f, dur: 2f,   amp: 0.25f, fade: true));
        Wav(absDir, "heavy_ambient",    Rumble  (freq: 75f,  dur: 2f,   amp: 0.40f));
        Wav(absDir, "ranged_ambient",   WNoise  (dur: 1.5f,  amp: 0.25f));
        Wav(absDir, "sentinel_ambient", PingDec (freq: 720f, dur: 0.8f, amp: 0.55f));
        Wav(absDir, "crafter_ambient",  Clicker (hz: 4f,    dur: 1f,   amp: 0.60f));
        Wav(absDir, "stalker_ambient",  Drone   (freq: 55f,  dur: 3f,   amp: 0.45f));
        Wav(absDir, "player_ambient",   SineWave(freq: 330f, dur: 1f,   amp: 0.08f, fade: true));

        // ---- Guns: fire ----
        Wav(absDir, "gun_sniper_fire",   Shot(sharp: 0.08f, boom: 0.55f, dur: 0.55f, amp: 0.85f));
        Wav(absDir, "gun_revolver_fire", Shot(sharp: 0.12f, boom: 0.40f, dur: 0.35f, amp: 0.80f));
        Wav(absDir, "gun_ar_fire",       Shot(sharp: 0.18f, boom: 0.25f, dur: 0.22f, amp: 0.70f));
        Wav(absDir, "gun_shotgun_fire",  Shot(sharp: 0.05f, boom: 0.75f, dur: 0.60f, amp: 0.90f));
        Wav(absDir, "gun_smg_fire",      Shot(sharp: 0.20f, boom: 0.18f, dur: 0.16f, amp: 0.65f));

        // ---- Guns: reload ----
        Wav(absDir, "gun_sniper_reload",   RldBolt  (dur: 0.50f, amp: 0.65f));
        Wav(absDir, "gun_revolver_reload", RldCyl   (dur: 0.45f, amp: 0.60f));
        Wav(absDir, "gun_ar_reload",       RldMag   (dur: 0.38f, amp: 0.60f));
        Wav(absDir, "gun_shotgun_reload",  RldPump  (dur: 0.45f, amp: 0.70f));
        Wav(absDir, "gun_smg_reload",      RldMag   (dur: 0.30f, amp: 0.55f));

        AssetDatabase.Refresh();
        AssignAll();
    }

    // ── WAV encoder ───────────────────────────────────────────────────────────

    static void Wav(string dir, string name, float[] s)
    {
        using var fs = new FileStream(Path.Combine(dir, name + ".wav"), FileMode.Create);
        using var bw = new BinaryWriter(fs);
        int db = s.Length * 2;
        bw.Write(new[]{'R','I','F','F'}); bw.Write(36 + db);
        bw.Write(new[]{'W','A','V','E'});
        bw.Write(new[]{'f','m','t',' '}); bw.Write(16);
        bw.Write((short)1); bw.Write((short)1); bw.Write(SR); bw.Write(SR * 2);
        bw.Write((short)2); bw.Write((short)16);
        bw.Write(new[]{'d','a','t','a'}); bw.Write(db);
        foreach (float v in s)
            bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
    }

    static string APath(string n) => $"Assets/{OutRel}/{n}.wav";
    static AudioClip Load(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>(APath(n));
    static int N(float dur) => Mathf.Max(1, (int)(SR * dur));
    static float Clamp(float v) => Mathf.Clamp(v, -1f, 1f);

    // ── Generators ────────────────────────────────────────────────────────────

    static float[] Whoosh(float freq, float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random((int)(freq * 1000));
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float alpha = Mathf.Clamp01(2f * Mathf.PI * freq / SR);
            prev += alpha * (noise - prev);
            d[i] = Clamp(amp * Mathf.Sin(Mathf.PI * i / n) * prev * 6f);
        }
        return d;
    }

    static float[] Impact(float freq, float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random((int)(freq * 100));
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float env = Mathf.Exp(-12f * t);
            float f = freq * (1f + 2f * Mathf.Exp(-30f * t));
            d[i] = Clamp(amp * env * (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * (float)(rng.NextDouble() * 2 - 1)));
        }
        return d;
    }

    static float[] Clang(float freq, float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float env = Mathf.Exp(-8f * t);
            d[i] = Clamp(amp * env * (
                0.5f * Mathf.Sin(2f * Mathf.PI * freq * t) +
                0.3f * Mathf.Sin(2f * Mathf.PI * freq * 2.76f * t) +
                0.2f * Mathf.Sin(2f * Mathf.PI * freq * 4.07f * t)));
        }
        return d;
    }

    static float[] Thump(float freq, float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random((int)(freq * 50));
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float sweep = freq * (1f + 2f * Mathf.Exp(-30f * t));
            d[i] = Clamp(amp * Mathf.Exp(-20f * t) * (Mathf.Sin(2f * Mathf.PI * sweep * t)
                        + 0.2f * (float)(rng.NextDouble() * 2 - 1)));
        }
        return d;
    }

    static float[] Click(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        for (int i = 0; i < n; i++)
        { float t = (float)i / SR; d[i] = Clamp(amp * Mathf.Exp(-120f * t) * Mathf.Sin(2f * Mathf.PI * 3000f * t)); }
        return d;
    }

    static float[] HurtBurst(float freq, float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random((int)(freq * 77));
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            d[i] = Clamp(amp * Mathf.Exp(-15f * t) * (Mathf.Sin(2f * Mathf.PI * freq * t * (1f - 0.4f * t))
                        + 0.4f * (float)(rng.NextDouble() * 2 - 1)));
        }
        return d;
    }

    static float[] DeathFade(float freq, float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random((int)(freq * 33));
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float fDrop = freq * Mathf.Exp(-2f * t);
            d[i] = Clamp(amp * Mathf.Exp(-5f * t) * (Mathf.Sin(2f * Mathf.PI * fDrop * t)
                        + 0.25f * (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-8f * t)));
        }
        return d;
    }

    static float[] AlertChirp(float freq, float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            d[i] = Clamp(amp * Mathf.Sin(Mathf.PI * i / n) * Mathf.Sin(2f * Mathf.PI * freq * (1f + 1.5f * t / dur) * t));
        }
        return d;
    }

    static float[] ElecGlitch(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(42);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float freq = 1800f + 600f * Mathf.Sin(2f * Mathf.PI * 40f * t);
            d[i] = Clamp(amp * Mathf.Exp(-20f * t) * (Mathf.Sin(2f * Mathf.PI * freq * t)
                        + 0.3f * (float)(rng.NextDouble() * 2 - 1)));
        }
        return d;
    }

    static float[] ElecShutdown(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float fDrop = 1400f * Mathf.Exp(-3f * t) + 100f;
            d[i] = Clamp(amp * Mathf.Exp(-4f * t) * (Mathf.Sin(2f * Mathf.PI * fDrop * t)
                        + 0.4f * Mathf.Sin(2f * Mathf.PI * fDrop * 0.5f * t)));
        }
        return d;
    }

    static float[] ElecAlert(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        float bd = 0.08f, gap = 0.04f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR, env = 0f;
            if (t < bd) env = Mathf.Sin(Mathf.PI * t / bd);
            float t2 = t - bd - gap;
            if (t2 >= 0 && t2 < bd * 0.8f) env = Mathf.Sin(Mathf.PI * t2 / (bd * 0.8f));
            d[i] = Clamp(amp * env * Mathf.Sin(2f * Mathf.PI * 1200f * t));
        }
        return d;
    }

    static float[] CrashNoise(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            d[i] = Clamp(amp * Mathf.Exp(-6f * t) * (
                0.5f * Mathf.Sin(2f * Mathf.PI * 120f * Mathf.Exp(-4f * t) * t) +
                0.5f * (float)(rng.NextDouble() * 2 - 1)));
        }
        return d;
    }

    static float[] HissHurt(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(13); float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            prev = prev * 0.85f + (float)(rng.NextDouble() * 2 - 1) * 0.15f;
            d[i] = Clamp(amp * Mathf.Exp(-10f * t) * (1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 8f * t)) * prev * 3f);
        }
        return d;
    }

    static float[] HissShriek(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(21);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float f = 400f + 1200f * Mathf.Exp(-5f * t) * Mathf.Sin(Mathf.PI * t / dur);
            d[i] = Clamp(amp * Mathf.Exp(-4f * t) * (Mathf.Sin(2f * Mathf.PI * f * t)
                        + 0.25f * (float)(rng.NextDouble() * 2 - 1)));
        }
        return d;
    }

    static float[] HissGrowl(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(33);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float wobble = 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 12f * t);
            d[i] = Clamp(amp * Mathf.Sin(Mathf.PI * i / n) * (Mathf.Sin(2f * Mathf.PI * 120f * wobble * t)
                        + 0.3f * (float)(rng.NextDouble() * 2 - 1)));
        }
        return d;
    }

    static float[] Shot(float sharp, float boom, float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random((int)(sharp * 10000));
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            d[i] = Clamp(amp * ((float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t / sharp)
                        + Mathf.Sin(2f * Mathf.PI * 60f * t) * Mathf.Exp(-t / boom) * 0.6f));
        }
        return d;
    }

    static float[] RldBolt(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(1);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float slide = t < dur * 0.5f ? Mathf.Sin(Mathf.PI * t / (dur * 0.5f)) * 0.4f * (float)(rng.NextDouble() * 2 - 1) : 0f;
            float t2 = t - dur * 0.7f;
            float ck = t2 >= 0 ? Mathf.Exp(-80f * t2) * Mathf.Sin(2f * Mathf.PI * 2200f * t2) : 0f;
            d[i] = Clamp(amp * (slide + ck));
        }
        return d;
    }

    static float[] RldCyl(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR, phase = t % 0.06f;
            d[i] = Clamp(amp * Mathf.Sin(Mathf.PI * i / n) * Mathf.Exp(-200f * phase) * Mathf.Sin(2f * Mathf.PI * 1800f * phase));
        }
        return d;
    }

    static float[] RldMag(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float e1 = Mathf.Exp(-60f * t) * Mathf.Sin(2f * Mathf.PI * 1600f * t);
            float t2 = t - dur * 0.45f; float e2 = t2 >= 0 ? Mathf.Exp(-70f * t2) * Mathf.Sin(2f * Mathf.PI * 1800f * t2) : 0f;
            float t3 = t - dur * 0.85f; float e3 = t3 >= 0 ? Mathf.Exp(-90f * t3) * Mathf.Sin(2f * Mathf.PI * 2400f * t3) : 0f;
            d[i] = Clamp(amp * (e1 + e2 + e3));
        }
        return d;
    }

    static float[] RldPump(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(99);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float env = Mathf.Sin(Mathf.PI * i / n) * Mathf.Exp(-4f * t);
            d[i] = Clamp(amp * env * ((float)(rng.NextDouble() * 2 - 1) * 0.6f + Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.4f));
        }
        return d;
    }

    static float[] SineWave(float freq, float dur, float amp, bool fade = false)
    {
        int n = N(dur); var d = new float[n];
        for (int i = 0; i < n; i++)
        { float t = (float)i / SR; d[i] = amp * (fade ? Mathf.Sin(Mathf.PI * i / n) : 1f) * Mathf.Sin(2f * Mathf.PI * freq * t); }
        return d;
    }

    static float[] Rumble(float freq, float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(42);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            d[i] = Clamp(amp * Mathf.Sin(Mathf.PI * i / n) * (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.2f * (float)(rng.NextDouble() * 2 - 1)));
        }
        return d;
    }

    static float[] WNoise(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(99);
        for (int i = 0; i < n; i++)
            d[i] = Clamp(amp * Mathf.Sin(Mathf.PI * i / n) * (float)(rng.NextDouble() * 2 - 1));
        return d;
    }

    static float[] PingDec(float freq, float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        for (int i = 0; i < n; i++)
        { float t = (float)i / SR; d[i] = Clamp(amp * Mathf.Exp(-6f * t) * Mathf.Sin(2f * Mathf.PI * freq * t)); }
        return d;
    }

    static float[] Clicker(float hz, float dur, float amp)
    {
        int n = N(dur); var d = new float[n]; float iv = 1f / hz;
        for (int i = 0; i < n; i++)
        {
            float phase = ((float)i / SR) % iv;
            d[i] = phase < 0.018f ? Clamp(amp * Mathf.Exp(-300f * phase) * Mathf.Sin(2f * Mathf.PI * 1400f * phase)) : 0f;
        }
        return d;
    }

    // Ranged enemy projectile fire: short sharp crack with energy tail
    static float[] RangedShot(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(55);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float crack = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t / 0.015f);
            float body  = Mathf.Sin(2f * Mathf.PI * 800f * t) * Mathf.Exp(-t / 0.06f);
            float tail  = Mathf.Sin(2f * Mathf.PI * 300f * t) * Mathf.Exp(-t / 0.12f) * 0.4f;
            d[i] = Clamp(amp * (crack * 0.5f + body + tail));
        }
        return d;
    }

    // Light melee swing (Patrol)
    static float[] LightSwing(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(61);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float alpha = Mathf.Clamp01(2f * Mathf.PI * 700f / SR);
            prev += alpha * (noise - prev);
            float env = Mathf.Sin(Mathf.PI * i / n);
            d[i] = Clamp(amp * env * prev * 5f);
        }
        return d;
    }

    // Heavy melee swing (Heavy)
    static float[] HeavySwing(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(72);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float alpha = Mathf.Clamp01(2f * Mathf.PI * 250f / SR);
            prev += alpha * (noise - prev);
            float env = Mathf.Sin(Mathf.PI * i / n);
            float low = Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.4f * Mathf.Exp(-8f * t);
            d[i] = Clamp(amp * (env * prev * 4f + low));
        }
        return d;
    }

    // Sentinel electric strike
    static float[] ElecStrike(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(83);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float env = Mathf.Exp(-15f * t);
            float freq = 2000f + 800f * Mathf.Sin(2f * Mathf.PI * 60f * t);
            d[i] = Clamp(amp * env * (Mathf.Sin(2f * Mathf.PI * freq * t)
                        + 0.4f * (float)(rng.NextDouble() * 2 - 1)));
        }
        return d;
    }

    // Crafter mechanical slam
    static float[] MechSlam(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(94);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float env = Mathf.Exp(-14f * t);
            float boom = Mathf.Sin(2f * Mathf.PI * 150f * t * (1f + Mathf.Exp(-20f * t)));
            float noise = 0.3f * (float)(rng.NextDouble() * 2 - 1);
            d[i] = Clamp(amp * env * (boom + noise));
        }
        return d;
    }

    // Stalker lunge hiss
    static float[] StalkerLunge(float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(17);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float noise = (float)(rng.NextDouble() * 2 - 1);
            prev = prev * 0.80f + noise * 0.20f;
            float env = Mathf.Sin(Mathf.PI * i / n) * (1f + 0.4f * Mathf.Sin(2f * Mathf.PI * 15f * t));
            d[i] = Clamp(amp * env * prev * 3.5f);
        }
        return d;
    }

    static float[] Drone(float freq, float dur, float amp)
    {
        int n = N(dur); var d = new float[n];
        var rng = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float w = 1f + 0.018f * Mathf.Sin(2f * Mathf.PI * 0.4f * t);
            d[i] = Clamp(amp * Mathf.Sin(Mathf.PI * i / n) * (
                0.45f * Mathf.Sin(2f * Mathf.PI * freq * w * t) +
                0.20f * Mathf.Sin(2f * Mathf.PI * freq * 2f * w * t) +
                0.04f * (float)(rng.NextDouble() * 2 - 1)));
        }
        return d;
    }

    // ── Assignment ────────────────────────────────────────────────────────────

    static void AssignAll()
    {
        int count = 0;
        count += AssignPlayer();
        count += AssignEnemies();
        count += AssignRangedFire();
        count += AssignAmbient();
        count += AssignGuns();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[AudioGen] Done — {count} field(s) filled. Save the scene to persist.");
    }

    static int AssignPlayer()
    {
        int c = 0;
        var go = GameObject.FindWithTag("Player");
        if (!go) { Debug.LogWarning("[AudioGen] Player not found."); return 0; }

        c += Set(go, "Voidborne.Combat.Melee.MeleeController", "swingClip",       "swing_whoosh");
        c += Set(go, "Voidborne.Combat.Melee.MeleeController", "hitClip",          "hit_impact");
        c += Set(go, "Voidborne.Combat.Melee.ParrySystem",     "parrySuccessClip", "parry_success");
        c += Set(go, "Voidborne.Combat.Melee.ParrySystem",     "parryFailClip",    "parry_fail");
        c += Set(go, "Voidborne.Combat.Melee.KickAbility",     "kickSwingClip",    "kick_swing");
        c += Set(go, "Voidborne.Combat.Melee.KickAbility",     "kickHitClip",      "kick_hit");
        c += Set(go, "Voidborne.Combat.Melee.FeintSystem",     "feintClip",        "feint_swoosh");
        c += Set(go, "Voidborne.Combat.Melee.FeintSystem",     "chamberClip",      "chamber_click");

        var feint = go.GetComponent("Voidborne.Combat.Melee.FeintSystem") as MonoBehaviour;
        if (feint)
        {
            var so = new SerializedObject(feint);
            var p = so.FindProperty("audioSource");
            if (p != null && p.objectReferenceValue == null)
            { p.objectReferenceValue = go.GetComponent<AudioSource>(); so.ApplyModifiedProperties(); EditorUtility.SetDirty(feint); c++; }
        }
        return c;
    }

    static int AssignRangedFire()
    {
        int c = 0;
#if UNITY_2023_1_OR_NEWER
        var mbs = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
#else
        var mbs = Object.FindObjectsOfType<MonoBehaviour>();
#endif
        var fireClip = Load("ranged_fire");
        if (!fireClip) { Debug.LogWarning("[AudioGen] ranged_fire.wav not found"); return 0; }
        foreach (var mb in mbs)
        {
            if (mb.GetType().FullName != "Voidborne.Enemies.RangedEnemyAttack") continue;
            var so = new SerializedObject(mb);
            var prop = so.FindProperty("_fireSound");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = fireClip;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(mb);
                Debug.Log($"[AudioGen] RangedEnemyAttack({mb.gameObject.name}): fire sound assigned");
                c++;
            }
        }
        return c;
    }

    static int AssignEnemies()
    {
        int c = 0;
#if UNITY_2023_1_OR_NEWER
        var mbs = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
#else
        var mbs = Object.FindObjectsOfType<MonoBehaviour>();
#endif
        foreach (var mb in mbs)
        {
            if (mb.GetType().FullName != "Voidborne.Enemies.EnemyEntity") continue;
            string n = mb.gameObject.name;
            string p = n.Contains("Patrol")   ? "patrol"   :
                       n.Contains("Heavy")    ? "heavy"    :
                       n.Contains("Ranged")   ? "ranged"   :
                       n.Contains("Sentinel") ? "sentinel" :
                       n.Contains("Crafter")  ? "crafter"  :
                       n.Contains("Stalker")  ? "stalker"  : null;
            if (p == null) continue;

            var so = new SerializedObject(mb);
            var src = so.FindProperty("_audioSource");
            if (src != null && src.objectReferenceValue == null)
            { src.objectReferenceValue = mb.gameObject.GetComponent<AudioSource>(); c++; }

            if (p != "ranged") c += SetSO(so, "_attackSound", p + "_attack");
            c += SetSO(so, "_hitSound",   p + "_hit");
            c += SetSO(so, "_deathSound", p + "_death");
            c += SetSO(so, "_alertSound", p + "_alert");
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(mb);
            Debug.Log($"[AudioGen] EnemyEntity({n}): {p} sounds assigned");
        }
        return c;
    }

    static int AssignAmbient()
    {
        int c = 0;
#if UNITY_2023_1_OR_NEWER
        var srcs = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
#else
        var srcs = Object.FindObjectsOfType<AudioSource>();
#endif
        foreach (var src in srcs)
        {
            string n = src.gameObject.name;
            string cn = n == "Player"          ? "player_ambient"   :
                        n.Contains("Patrol")   ? "patrol_ambient"   :
                        n.Contains("Heavy")    ? "heavy_ambient"    :
                        n.Contains("Ranged")   ? "ranged_ambient"   :
                        n.Contains("Sentinel") ? "sentinel_ambient" :
                        n.Contains("Crafter")  ? "crafter_ambient"  :
                        n.Contains("Stalker")  ? "stalker_ambient"  : null;
            if (cn == null) continue;
            var clip = Load(cn);
            if (clip && src.clip != clip) { src.clip = clip; EditorUtility.SetDirty(src); c++; }
        }
        return c;
    }

    static int AssignGuns()
    {
        int c = 0;
        var guns = new (string path, string fire, string reload)[]
        {
            ("Assets/ScriptableObjects/Guns/BoltSniper.asset",       "gun_sniper_fire",   "gun_sniper_reload"),
            ("Assets/ScriptableObjects/Guns/HandmadeRevolver.asset", "gun_revolver_fire", "gun_revolver_reload"),
            ("Assets/ScriptableObjects/Guns/Ironbark_AR.asset",      "gun_ar_fire",       "gun_ar_reload"),
            ("Assets/ScriptableObjects/Guns/PumpShotgun.asset",      "gun_shotgun_fire",  "gun_shotgun_reload"),
            ("Assets/ScriptableObjects/Guns/Rattler_SMG.asset",      "gun_smg_fire",      "gun_smg_reload"),
        };
        foreach (var (ap, fn, rn) in guns)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ap);
            if (!asset) { Debug.LogWarning($"[AudioGen] Missing: {ap}"); continue; }
            var so = new SerializedObject(asset);
            c += SetSO(so, "fireSound", fn);
            c += SetSO(so, "reloadSound", rn);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            Debug.Log($"[AudioGen] GunDef {asset.name}: fire + reload assigned");
        }
        AssetDatabase.SaveAssets();
        return c;
    }

    static int Set(GameObject go, string type, string field, string clip)
    {
        var mb = go.GetComponent(type) as MonoBehaviour;
        if (!mb) return 0;
        var so = new SerializedObject(mb);
        int r = SetSO(so, field, clip);
        so.ApplyModifiedProperties();
        if (r > 0) EditorUtility.SetDirty(mb);
        return r;
    }

    static int SetSO(SerializedObject so, string field, string clipName)
    {
        var prop = so.FindProperty(field);
        if (prop == null) return 0;
        var clip = Load(clipName);
        if (!clip) { Debug.LogWarning($"[AudioGen] Not found: {clipName}.wav"); return 0; }
        if (prop.objectReferenceValue == clip) return 0;
        prop.objectReferenceValue = clip;
        return 1;
    }
}
