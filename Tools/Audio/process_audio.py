"""SafeRoute VR audio tool (offline, no downloads, no online services).

  python Tools/Audio/process_audio.py voice    trim + loudness-normalise the instructor voice WAVs in place
  python Tools/Audio/process_audio.py sfx      synthesise the fire and alarm sounds
  python Tools/Audio/process_audio.py report   print duration / peak / RMS / loop-seam stats for every clip

The fire and alarm sounds are procedurally synthesised by this script (original work, no third-party
recordings). They are believable but not recorded; replace them with licensed field recordings of the
same file names for maximum realism (see Assets/SafeRoute/Audio/README.md).
Requires numpy (and scipy for the voice high-pass filter).
"""
import glob
import os
import sys
import wave

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
AUDIO = os.path.join(ROOT, "Assets", "SafeRoute", "Audio")
VOICE = os.path.join(AUDIO, "Voice")
SFX = os.path.join(AUDIO, "SFX")
RATE = 44100


def db(x):
    return 20 * np.log10(max(x, 1e-9))


def read(path):
    with wave.open(path) as w:
        assert w.getsampwidth() == 2 and w.getnchannels() == 1, path
        data = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64) / 32768.0
        return data, w.getframerate()


def write(path, data, rate):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    pcm = np.clip(np.round(data * 32767.0), -32768, 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(pcm.tobytes())


def fade(data, rate, fade_in=0.01, fade_out=0.02):
    n_in, n_out = int(rate * fade_in), int(rate * fade_out)
    data = data.copy()
    if n_in:
        data[:n_in] *= np.linspace(0.0, 1.0, n_in)
    if n_out:
        data[-n_out:] *= np.linspace(1.0, 0.0, n_out)
    return data


# ---------- Voice ----------

def process_voice():
    from scipy.signal import butter, sosfiltfilt

    files = sorted(glob.glob(os.path.join(VOICE, "instructor_*.wav")))
    if not files:
        sys.exit("No voice files found in " + VOICE)
    for path in files:
        data, rate = read(path)
        data = sosfiltfilt(butter(2, 80, "highpass", fs=rate, output="sos"), data)  # remove rumble / DC
        # Trim silence (below -45 dBFS envelope), keeping short pads so speech starts promptly.
        env = np.convolve(np.abs(data), np.ones(int(rate * 0.01)) / (rate * 0.01), mode="same")
        loud = np.nonzero(env > 10 ** (-45 / 20))[0]
        start = max(0, loud[0] - int(rate * 0.04))
        end = min(len(data), loud[-1] + int(rate * 0.12))
        data = data[start:end]
        # Consistent loudness across lines: RMS of voiced parts to -19 dBFS, peaks limited to -3 dBFS.
        voiced = data[np.abs(data) > 10 ** (-40 / 20)]
        data *= 10 ** (-19 / 20) / np.sqrt(np.mean(voiced ** 2))
        peak = np.max(np.abs(data))
        if peak > 10 ** (-3 / 20):
            data *= 10 ** (-3 / 20) / peak
        write(path, fade(data, rate, 0.005, 0.03), rate)
        print(f"voice {os.path.basename(path)}: {len(data) / rate:.2f}s")


# ---------- Synthesis helpers ----------

def band_noise(rng, n, low, high, tilt=0.0):
    """Circular (seamlessly looping) noise limited to [low, high] Hz with a 1/f^tilt slope."""
    spectrum = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / RATE)
    shape = np.zeros_like(f)
    band = (f >= low) & (f <= high)
    shape[band] = (np.maximum(f[band], 1.0) / max(low, 1.0)) ** (-tilt / 2)
    # Soft band edges.
    edge = 0.15
    shape *= np.clip((f - low * (1 - edge)) / (low * edge + 1e-9), 0, 1) if low > 0 else 1
    shape *= np.clip((high * (1 + edge) - f) / (high * edge), 0, 1)
    out = np.fft.irfft(spectrum * shape, n)
    return out / np.sqrt(np.mean(out ** 2))


def slow_envelope(rng, n, max_hz, depth):
    """Circular smooth random envelope around 1.0."""
    e = band_noise(rng, n, 0.05, max_hz)
    return np.clip(1.0 + depth * e / np.max(np.abs(e)), 0.0, None)


def grain(rng, length_s, decay_s, bright):
    n = max(8, int(RATE * length_s))
    t = np.arange(n) / RATE
    g = rng.standard_normal(n) * np.exp(-t / decay_s)
    if bright < 1.0:  # simple one-pole smoothing for duller crackles
        a = 1.0 - bright
        for i in range(1, n):
            g[i] += a * g[i - 1]
    g[:3] *= np.array([0.3, 0.7, 1.0])[: min(3, n)]
    return g / (np.max(np.abs(g)) + 1e-9)


def highpass_circular(x, cutoff):
    spectrum = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / RATE)
    spectrum *= 1.0 / np.sqrt(1.0 + (cutoff / np.maximum(f, 1.0)) ** 4)
    return np.fft.irfft(spectrum, len(x))


# ---------- Fire ----------

def fire_loop(seconds=12.0, seed=11):
    rng = np.random.default_rng(seed)
    n = int(RATE * seconds)
    # Low roar of the flames: brown-ish noise, slowly breathing.
    roar = band_noise(rng, n, 35, 420, tilt=1.2) * slow_envelope(rng, n, 1.2, 0.45) * 10 ** (-27 / 20)
    # Airy hiss of burning gases.
    hiss = band_noise(rng, n, 1800, 7500, tilt=0.5) * slow_envelope(rng, n, 6.0, 0.6) * 10 ** (-41 / 20)
    # Crackles: short decaying noise grains, clustered in time, mostly small with occasional loud ones.
    crackle = np.zeros(n)
    density = slow_envelope(rng, n, 0.8, 0.7)
    t = 0.0
    while t < seconds:
        t += rng.exponential(1.0 / 22.0)
        i = int(t * RATE) % n
        if rng.random() > min(1.0, density[i] / 1.4):
            continue
        amp = min(1.0, 0.05 * rng.pareto(1.6) + 0.02)
        g = grain(rng, rng.uniform(0.002, 0.018), rng.uniform(0.0008, 0.005), rng.uniform(0.35, 1.0)) * amp
        idx = (i + np.arange(len(g))) % n  # wrap round: the loop stays seamless
        crackle[idx] += g
        if rng.random() < 0.25:  # crackles often come in quick bursts
            for _ in range(rng.integers(1, 4)):
                j = (i + int(rng.uniform(0.004, 0.03) * RATE)) % n
                g2 = grain(rng, 0.004, rng.uniform(0.0006, 0.002), 1.0) * amp * rng.uniform(0.3, 0.8)
                crackle[(j + np.arange(len(g2))) % n] += g2
    crackle = highpass_circular(crackle, 700) * 0.55
    mix = roar + hiss + crackle
    return mix * (10 ** (-6 / 20) / np.max(np.abs(mix)))


def fire_pop(seed):
    rng = np.random.default_rng(seed)
    n = int(RATE * 0.45)
    t = np.arange(n) / RATE
    out = np.zeros(n)
    # Sharp crack (resonant band noise), a few secondary snaps and a soft low thump.
    crack = band_noise(rng, n, 900, 5200, tilt=0.6) * np.exp(-t / rng.uniform(0.012, 0.025))
    out += crack
    for _ in range(rng.integers(3, 8)):
        start = int(rng.uniform(0.0, 0.06) * RATE)
        g = grain(rng, 0.01, rng.uniform(0.0008, 0.003), 1.0) * rng.uniform(0.3, 0.9)
        out[start:start + len(g)] += g[: n - start]
    out += 0.5 * np.sin(2 * np.pi * rng.uniform(70, 110) * t) * np.exp(-t / 0.05) * (1 - np.exp(-t / 0.002))
    out[: int(RATE * 0.0004)] *= np.linspace(0, 1, int(RATE * 0.0004))
    out = fade(out, RATE, 0.0, 0.08)
    return out * (10 ** (-3 / 20) / np.max(np.abs(out)))


# ---------- Alarm ----------

def alarm_temporal3():
    """ISO 8201 'temporal-3' evacuation pattern: three 0.5 s tones, 0.5 s gaps, then 1.5 s pause (4 s loop).
    A warm 950 Hz electronic sounder tone with soft edges (no harsh square wave)."""
    n = int(RATE * 4.0)
    t = np.arange(n) / RATE
    f0 = 950.0
    tone = (np.sin(2 * np.pi * f0 * t) + 0.25 * np.sin(2 * np.pi * 2 * f0 * t) + 0.12 * np.sin(2 * np.pi * 3 * f0 * t)
            + 0.05 * np.sin(2 * np.pi * 5 * f0 * t))
    gate = np.zeros(n)
    ramp = int(RATE * 0.008)
    for k in range(3):
        a, b = int(RATE * k), int(RATE * (k + 0.5))
        gate[a:b] = 1.0
        gate[a:a + ramp] = np.linspace(0, 1, ramp)
        gate[b - ramp:b] = np.linspace(1, 0, ramp)
    out = tone * gate
    return out * (10 ** (-6 / 20) / np.max(np.abs(out)))


def fire_contact_burst(seed=7):
    """Short non-graphic 'fire contact' impact: a fast flame whoosh (swelling band noise), a soft low thump and a
    few crackles, decaying over about 1.2 s. Played in 2D when the trainee walks into the fire."""
    rng = np.random.default_rng(seed)
    n = int(RATE * 1.4)
    t = np.arange(n) / RATE
    env = (1 - np.exp(-t / 0.03)) * np.exp(-t / 0.35)
    whoosh = band_noise(rng, n, 150, 3500, tilt=1.0) * env
    thump = 0.8 * np.sin(2 * np.pi * 55 * t + 6 * np.exp(-t / 0.04)) * np.exp(-t / 0.12) * (1 - np.exp(-t / 0.004))
    crackle = np.zeros(n)
    for _ in range(14):
        start = int(rng.uniform(0.02, 0.7) * RATE)
        g = grain(rng, 0.01, rng.uniform(0.0008, 0.003), 1.0) * rng.uniform(0.2, 0.6) * np.exp(-start / RATE / 0.4)
        crackle[start:start + len(g)] += g[: n - start]
    out = whoosh + thump + highpass_circular(crackle, 900) * 0.6
    out = fade(out, RATE, 0.002, 0.25)
    return out * (10 ** (-3 / 20) / np.max(np.abs(out)))


def make_sfx():
    write(os.path.join(SFX, "fire_crackle_loop.wav"), fire_loop(), RATE)
    for k in range(4):
        write(os.path.join(SFX, f"fire_pop_{k + 1:02d}.wav"), fire_pop(100 + k), RATE)
    write(os.path.join(SFX, "alarm_temporal3_loop.wav"), alarm_temporal3(), RATE)
    write(os.path.join(SFX, "fire_contact_burst.wav"), fire_contact_burst(), RATE)


def report():
    for path in sorted(glob.glob(os.path.join(AUDIO, "**", "*.wav"), recursive=True)):
        data, rate = read(path)
        seam = abs(data[0] - data[-1])
        print(f"{os.path.relpath(path, AUDIO):45s} {len(data) / rate:6.2f}s {rate:6d}Hz  peak {db(np.max(np.abs(data))):6.1f} dBFS"
              f"  rms {db(np.sqrt(np.mean(data ** 2))):6.1f} dBFS  dc {np.mean(data):+.4f}  seam {seam:.4f}")


if __name__ == "__main__":
    command = sys.argv[1] if len(sys.argv) > 1 else "report"
    {"voice": process_voice, "sfx": make_sfx, "report": report}[command]()
