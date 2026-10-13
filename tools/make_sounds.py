#!/usr/bin/env python3
"""Synthesises AVAMoonPatrol's sound effects and tunes into ../src/AVAMoonPatrol/Sounds as 16-bit mono WAV.

Nothing is sampled from any arcade machine: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVAMoonPatrol", "Sounds")
RNG = random.Random(1982)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def hats(bpm, bars, pattern="x.x.x.x.", gain=0.18):
    """Light noise ticks on the off-beats, eight to a bar."""
    step = 60.0 / bpm / 2
    out = []
    for _ in range(bars):
        for ch in pattern:
            out.extend(tone(step, 7000, 6000, "noise", attack=0.001, release=6, gain=gain) if ch == "x" else silence(step))
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    # Both cannons: a short twin zap (it fires a lot, so it's quiet and quick).
    write("Fire.wav", mix(tone(0.07, 2200, 700, "square", release=1.8), tone(0.07, 1500, 3000, "pulse", release=2, gain=0.5)), 0.3)
    write("Jump.wav", tone(0.22, 260, 720, "pulse", attack=0.005, release=0.6), 0.4)
    write("Land.wav", tone(0.07, 180, 90, "noise", release=1.5), 0.3)
    write("RockHit.wav", cat(tone(0.03, 900, 600, "square"), tone(0.08, 400, 200, "noise", release=1.4)), 0.45)
    write("RockBreak.wav", cat(tone(0.04, 1200, 500, "square"), tone(0.3, 500, 60, "noise", release=1.1)), 0.6)
    # The flight arriving: a warbling whistle.
    write("UfoArrive.wav", tone(0.7, 900, 1400, "tri", attack=0.02, release=0.4, vibrato=0.08, vib_rate=11), 0.4)
    write("UfoHit.wav", cat(tone(0.06, 1800, 2600, "square"), tone(0.25, 1200, 100, "noise", release=1.0)), 0.55)
    write("BombBlast.wav", tone(0.3, 300, 40, "noise", release=1.0), 0.5)
    write("Crater.wav", cat(tone(0.08, 160, 60, "square"), tone(0.45, 220, 30, "noise", release=0.8)), 0.65)
    write("TankFire.wav", cat(tone(0.05, 120, 80, "square"), tone(0.15, 600, 150, "noise", release=1.3)), 0.5)
    write("TankHit.wav", cat(tone(0.08, 200, 500, "saw"), tone(0.5, 500, 40, "noise", release=0.8)), 0.65)
    write("ShellHit.wav", tone(0.12, 1500, 300, "noise", release=1.3), 0.4)
    write("Crash.wav", cat(*[tone(0.07, 900 - i * 70, 820 - i * 70, "square", release=0.5) for i in range(8)], tone(0.8, 350, 30, "noise", release=0.7)), 0.7)
    write("Checkpoint.wav", cat(tone(0.08, hz("A5"), kind="pulse", release=0.4), tone(0.16, hz("E6"), kind="pulse", release=0.5)), 0.4)
    write("Bonus.wav", tune(200,
        [("C5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 0.5), ("G5", 0.5), ("C6", 1.5)],
        [("C3", 1), ("G3", 1), ("C3", 2)]), 0.55)
    write("JumpPoints.wav", tone(0.06, 1700, 2300, "pulse", release=0.8), 0.3)
    write("ExtraLife.wav", cat(*[tone(0.06, hz(n), kind="square", release=0.4) for n in ["A5", "C#6", "E6", "A6"] * 3]), 0.45)

    # The driving music: a bouncing bass groove in A minor (new), 8 bars, loops while the buggy drives.
    bpm = 132
    groove = []
    for root, fifth, up in [("A2", "E3", "A3"), ("A2", "E3", "G3"), ("F2", "C3", "F3"), ("G2", "D3", "G3"),
                            ("A2", "E3", "A3"), ("A2", "E3", "C4"), ("D3", "A3", "D4"), ("E2", "B2", "E3")]:
        groove += [(root, 0.5), (root, 0.25), ("-", 0.25), (fifth, 0.5), (root, 0.5), (up, 0.5), (root, 0.5), (fifth, 0.5), (up, 0.5)]
    lead = [("-", 4), ("E5", 0.5), ("-", 0.5), ("E5", 0.5), ("G5", 0.5), ("A5", 1), ("-", 1),
            ("-", 4), ("C6", 0.5), ("-", 0.5), ("B5", 0.5), ("G5", 0.5), ("A5", 1), ("-", 1),
            ("-", 4), ("E5", 0.5), ("-", 0.5), ("E5", 0.5), ("G5", 0.5), ("A5", 0.5), ("C6", 0.5), ("D6", 1),
            ("C6", 0.5), ("B5", 0.5), ("A5", 0.5), ("G5", 0.5), ("E5", 2), ("D5", 0.5), ("E5", 0.5), ("G#5", 0.5), ("B5", 0.5), ("A5", 2)]
    beat = 60.0 / bpm
    bass_line = []
    for note, beats in groove:
        bass_line.extend(silence(beats * beat) if note == "-" else tone(beats * beat, hz(note), kind="square", release=1.6, gain=0.5))
    lead_line = []
    for note, beats in lead:
        lead_line.extend(silence(beats * beat) if note == "-" else tone(beats * beat, hz(note), kind="pulse", release=0.8, gain=0.22))
    write("Drive.wav", mix(bass_line, lead_line, hats(bpm, 8)), 0.55)

    # The title tune: a spacey march in A minor (new), 16 bars, loops.
    a = [("A4", 0.5), ("C5", 0.5), ("E5", 0.5), ("A5", 0.5), ("G5", 1), ("E5", 1)]
    b = [("F5", 0.5), ("E5", 0.5), ("D5", 0.5), ("C5", 0.5), ("D5", 1), ("E5", 1)]
    c = [("C6", 1), ("B5", 0.5), ("A5", 0.5), ("G5", 1), ("E5", 1)]
    d = [("F5", 0.5), ("G5", 0.5), ("A5", 0.5), ("B5", 0.5), ("C6", 1), ("B5", 1)]
    end = [("A5", 0.5), ("G#5", 0.5), ("B5", 0.5), ("G#5", 0.5), ("A5", 2)]
    lead = a + b + a + [("F5", 0.5), ("E5", 0.5), ("D5", 0.5), ("B4", 0.5), ("A4", 2)] \
        + c + d + c + [("D6", 0.5), ("C6", 0.5), ("B5", 0.5), ("G#5", 0.5), ("E5", 2)] \
        + a + b + a + end + d + c + [("F5", 1), ("D5", 0.5), ("B4", 0.5), ("E5", 2)] + end
    bass = [(n, 0.5) for bar in ["A2", "A2", "F2", "G2", "A2", "D3", "F2", "E2", "C3", "G2", "C3", "E2", "A2", "D3", "F2", "A2"]
            for n in [bar, bar[0] + str(int(bar[-1]) + 1) if len(bar) == 2 else bar] * 4]
    write("TitleTune.wav", tune(126, lead, bass), 0.6)

    # Setting off: a quick rising call.
    write("CourseStart.wav", tune(168,
        [("E5", 0.5), ("A5", 0.5), ("C6", 0.5), ("E6", 0.5), ("D6", 0.5), ("E6", 1.5)],
        [("A2", 1), ("E3", 1), ("A3", 2)]), 0.6)
    write("GameOver.wav", tune(88,
        [("E5", 1), ("C5", 1), ("A4", 1), ("G#4", 1), ("A4", 2), ("-", 1)],
        [("A2", 2), ("F2", 2), ("E2", 2), ("A2", 1)], kind="tri"), 0.65)
    print("sounds written to", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
