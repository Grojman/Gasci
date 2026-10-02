using Microsoft.Xna.Framework.Audio;

namespace Gasci.Audio;

/// <summary>
/// Procedural placeholder sounds so the game is not silent before real audio exists.
/// Every loop length is a multiple of its LFO periods so music loops without clicks.
/// </summary>
public static class Synth
{
    private const int Rate = 22050;
    private const double Tau = Math.PI * 2;

    public static SoundEffect? Sfx(string id) => id switch
    {
        "cursor" => Make(Render(0.025, t => Square(t, 1400) * Math.Exp(-t * 160) * 0.5)),
        "step" => Make(LowPass(Render(0.06, t => Noise() * Math.Exp(-t * 70)), 0.08)),
        "select" => Make(Render(0.07, t => Math.Sin(Tau * 660 * t) * Math.Exp(-t * 45) * 0.6)),
        "confirm" => Make(Render(0.16, t => Math.Sin(Tau * (t < 0.07 ? 520 : 780) * t) * Math.Exp(-t * 18) * 0.6)),
        "locked" => Make(Render(0.18, t => Square(t, 110) * Math.Exp(-t * 14) * 0.35)),
        "page" => Make(LowPass(Render(0.05, t => Noise() * Math.Exp(-t * 90) * 0.5), 0.5)),
        "save" => Make(Render(0.45, t => Math.Sin(Tau * (t < 0.15 ? 440 : t < 0.3 ? 554 : 659) * t) * Math.Exp(-(t % 0.15) * 12) * 0.5)),
        "door" => Make(LowPass(Render(0.5, t =>
            Noise() * Math.Exp(-t * 9) * 0.6 + Math.Sin(Tau * (170 + 30 * Math.Sin(Tau * 7 * t)) * t) * 0.25 * Math.Exp(-t * 4)), 0.15)),
        "creature" => Make(LowPass(Render(1.2, t =>
        {
            double env = Math.Sin(Math.PI * t / 1.2);
            double growl = Saw(t, 62 + 6 * Math.Sin(Tau * 9 * t)) * 0.6 + Noise() * 0.4;
            return growl * env * 0.8;
        }), 0.12)),
        _ => null,
    };

    public static SoundEffect? Music(string id) => id switch
    {
        // Low drone with a slow swell.
        "title" => Make(Render(12, t =>
            (Math.Sin(Tau * 55 * t) + 0.6 * Math.Sin(Tau * 82.5 * t) + 0.3 * Math.Sin(Tau * 110.5 * t))
            * (0.55 + 0.45 * Math.Sin(Tau * t / 6)) * 0.25)),

        // Drone plus a wall clock ticking once per second.
        "home" => Make(Render(8, t =>
        {
            double drone = (Math.Sin(Tau * 65.4 * t) + 0.5 * Math.Sin(Tau * 98 * t)) * (0.6 + 0.4 * Math.Sin(Tau * t / 4)) * 0.18;
            double beat = t % 1.0;
            double tick = beat < 0.02 ? Math.Sin(Tau * 2200 * beat) * (1 - beat / 0.02) * 0.25 : 0;
            return drone + tick;
        })),

        // Wind: noise through a low-pass whose cutoff breathes.
        "street" => Make(Wind(12)),

        "park" => Make(Render(12, t =>
            Math.Sin(Tau * 73.4 * t) * 0.15 + Math.Sin(Tau * 880 * t) * 0.03 * Math.Max(0, Math.Sin(Tau * t / 3)))),

        // Dissonant beating + heartbeat for the creature encounters.
        "shadow" => Make(Render(8, t =>
        {
            double drone = (Math.Sin(Tau * 60 * t) + Math.Sin(Tau * 63.75 * t)) * 0.2;
            double b = t % 1.0;
            double heart = (b < 0.08 ? Math.Sin(Tau * 50 * b) * (1 - b / 0.08) : 0)
                         + (b is > 0.2 and < 0.26 ? Math.Sin(Tau * 45 * (b - 0.2)) * (1 - (b - 0.2) / 0.06) * 0.7 : 0);
            return drone + heart * 0.6;
        })),
        _ => null,
    };

    private static float[] Render(double seconds, Func<double, double> wave)
    {
        var samples = new float[(int)(seconds * Rate)];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (float)wave(i / (double)Rate);
        return samples;
    }

    private static float[] Wind(double seconds)
    {
        var samples = new float[(int)(seconds * Rate)];
        double y = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double t = i / (double)Rate;
            double cutoff = 0.01 + 0.03 * (0.5 + 0.5 * Math.Sin(Tau * t / 4)) + 0.01 * Math.Sin(Tau * t / 1.5);
            y += cutoff * (Noise() - y);
            samples[i] = (float)(y * 2.2);
        }
        // Fade the edges so the loop point does not click.
        int fade = Rate / 10;
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            samples[i] *= k;
            samples[^(i + 1)] *= k;
        }
        return samples;
    }

    private static float[] LowPass(float[] samples, double alpha)
    {
        double y = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            y += alpha * (samples[i] - y);
            samples[i] = (float)(y / Math.Sqrt(alpha));
        }
        return samples;
    }

    private static double Square(double t, double freq) => Math.Sin(Tau * freq * t) >= 0 ? 1 : -1;
    private static double Saw(double t, double freq) => 2 * (t * freq - Math.Floor(0.5 + t * freq));

    [ThreadStatic] private static Random? _noise;
    private static double Noise() => (_noise ??= new Random(1234)).NextDouble() * 2 - 1;

    private static SoundEffect Make(float[] samples)
    {
        var buffer = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short s = (short)(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            buffer[i * 2] = (byte)(s & 0xFF);
            buffer[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }
        return new SoundEffect(buffer, Rate, AudioChannels.Mono);
    }
}
