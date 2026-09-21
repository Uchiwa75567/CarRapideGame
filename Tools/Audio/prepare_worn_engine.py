"""Build seamless PCM layers from Joseph Sardin's CC0 truck field recording.

Usage: python prepare_worn_engine.py source.wav output_dir (requires NumPy).
Source: https://lasonotheque.org/moteur-de-camion-1-s1442.html
All layers use recorded sound; no synthesized tones.
"""
import argparse
from pathlib import Path
import wave
import numpy as np


def read_pcm(path):
    with wave.open(str(path)) as source:
        rate, channels, width = source.getframerate(), source.getnchannels(), source.getsampwidth()
        raw = source.readframes(source.getnframes())
    if width == 3:
        b = np.frombuffer(raw, np.uint8).reshape(-1, 3).astype(np.int32)
        value = b[:, 0] | (b[:, 1] << 8) | (b[:, 2] << 16)
        value = np.where(value & 0x800000, value - 0x1000000, value) / 8388608
    elif width == 2:
        value = np.frombuffer(raw, '<i2') / 32768
    else:
        raise ValueError('Expected 16- or 24-bit PCM source')
    return value.reshape(-1, channels).mean(axis=1), rate


def layer(source, rate, start, duration, low, high, peak, drive=1):
    overlap = round(rate * .22)
    samples = round(duration * rate)
    clip = source[round(start * rate):round(start * rate) + samples + overlap].copy()
    if len(clip) != samples + overlap:
        raise ValueError('Source is too short')
    clip -= clip.mean()
    frequency = np.fft.rfftfreq(len(clip), 1 / rate)
    response = (frequency / np.maximum(frequency + low, 1)) ** 2
    response *= 1 / (1 + (frequency / high) ** 6)
    clip = np.fft.irfft(np.fft.rfft(clip) * response, n=len(clip))
    clip = np.tanh(clip * drive)
    fade = .5 - .5 * np.cos(np.linspace(0, np.pi, overlap))
    seam = clip[-overlap:] * (1 - fade) + clip[:overlap] * fade
    result = np.concatenate([seam, clip[overlap:-overlap]])
    result *= peak / max(np.max(np.abs(result)), 1e-9)
    # Match the boundary within half a millisecond to remove a loop click.
    n = 24
    join = (result[0] + result[-1]) * .5
    result[:n] += (join - result[0]) * np.linspace(1, 0, n)
    result[-n:] += (join - result[-1]) * np.linspace(0, 1, n)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    source, rate = read_pcm(args.source)
    args.output.mkdir(parents=True, exist_ok=True)
    settings = {
        'Engine_Idle': (1.4, 8.0, 28, 1900, .62, 1.15),
        'Engine_Loop': (4.0, 8.0, 42, 4100, .68, 1.5),
        'Engine_Rattle': (2.5, 6.0, 850, 4700, .48, 1.0),
    }
    for name, values in settings.items():
        data = layer(source, rate, *values)
        assert np.isfinite(data).all() and max(abs(data)) < 1
        with wave.open(str(args.output / (name + '.wav')), 'wb') as output:
            output.setparams((1, 2, rate, len(data), 'NONE', 'not compressed'))
            output.writeframes(np.round(data * 32767).astype('<i2').tobytes())
        print(f'{name}: {len(data)/rate:.2f}s, peak={max(abs(data)):.3f}, rms={np.sqrt(np.mean(data**2)):.3f}, seam={abs(data[0]-data[-1]):.6f}')


if __name__ == '__main__':
    main()
