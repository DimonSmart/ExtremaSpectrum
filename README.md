# ExtremaSpectrum

ExtremaSpectrum is a .NET library for audio signal analysis based on an extrema-driven oscillation decomposition algorithm.

> This is not an FFT spectrum. The output describes detected local oscillations by frequency, not orthogonal sinusoidal components. See [Algorithm](#algorithm).

Repository: [github.com/DimonSmart/ExtremaSpectrum](https://github.com/DimonSmart/ExtremaSpectrum)

## Install

```bash
dotnet add package ExtremaSpectrum
```

The current package targets `.NET 10`.

## Quick start

```csharp
using ExtremaSpectrum;

var analyzer = new ExtremaSpectrumAnalyzer(new ExtremaSpectrumOptions
{
    BinCount = 128,
    MinFrequencyHz = 500f,
    MaxFrequencyHz = 8000f,
    MaxPasses = 12
});

AnalysisResult result = analyzer.Analyze(samples, sampleRate);
float[] spectrum = result.Spectrum;
```

## Streaming

```csharp
var analyzer = new StreamingExtremaSpectrumAnalyzer(
    options,
    analysisWindowSamples: 2048,
    hopSamples: 512);

if (analyzer.PushPcm16(buffer, format, out var result))
{
    float[] spectrum = result!.Spectrum;
}

if (analyzer.PushDetailedPcm16(buffer, format, out var report))
{
    IReadOnlyList<ExtremaPassSnapshot> passes = report!.Passes;
}
```

## Algorithm

The analyzer performs iterative decomposition of a discrete signal into local oscillations.

### One pass

1. Find all local extrema using strict neighbour comparison. Boundary points are excluded.
2. Scan left to right for consecutive triples in the form `min -> max -> min` or `max -> min -> max`.
3. For each accepted triple `(left, mid, right)`:
   - `periodSamples = right - left`
   - `L = floor(midpoint(left, mid))`
   - `R = ceil(midpoint(mid, right))`
   - when the next accepted triple reuses the same slope, its left boundary starts from the already chosen `R`
   - `baseline = (signal[left] + signal[right]) / 2`
   - `amplitude = abs(signal[mid] - baseline)`
   - `frequencyHz = sampleRate / periodSamples`
4. If the oscillation passes the period and amplitude filters and maps into a valid bin, its contribution is added to the spectrum.
5. Samples strictly between `L` and `R` are removed from later passes, while the midpoint boundary samples are preserved.
6. The scan advances by one extremum, so adjacent accepted oscillations can share the same preserved midpoint boundary.

Multiple passes are performed until no valid triple is found or `MaxPasses` is reached.

### Extrema detection

```text
local max: signal[i - 1] < signal[i] && signal[i] >= signal[i + 1]
local min: signal[i - 1] > signal[i] && signal[i] <= signal[i + 1]
```

Flat plateaus use the first point that satisfies the comparison.

### Frequency binning

```text
binWidth = (MaxFrequencyHz - MinFrequencyHz) / BinCount
binIndex = floor((frequencyHz - MinFrequencyHz) / binWidth)
```

## Detailed trace and SVG export

`AnalyzeDetailed(...)` returns an `ExtremaAnalysisReport` with per-pass spectra, accepted oscillations, support ranges, and waveform snapshots.

The demo app can also export step-by-step SVG frames:

```bash
dotnet run --project src/ExtremaSpectrum.Demo -- --input data/demo-low-010hz-plus-high-160hz.wav --export-step-images temp
```

Example frames generated from `data/demo-low-010hz-plus-high-160hz.wav`:

![Initial waveform](https://raw.githubusercontent.com/DimonSmart/ExtremaSpectrum/main/docs/readme-assets/demo-low-plus-high-step-00-initial.png)

![After pass 1](https://raw.githubusercontent.com/DimonSmart/ExtremaSpectrum/main/docs/readme-assets/demo-low-plus-high-step-01-pass-01.png)

![After pass 2](https://raw.githubusercontent.com/DimonSmart/ExtremaSpectrum/main/docs/readme-assets/demo-low-plus-high-step-02-pass-02.png)

![After pass 3](https://raw.githubusercontent.com/DimonSmart/ExtremaSpectrum/main/docs/readme-assets/demo-low-plus-high-step-03-pass-03.png)

![After pass 4](https://raw.githubusercontent.com/DimonSmart/ExtremaSpectrum/main/docs/readme-assets/demo-low-plus-high-step-04-pass-04.png)

## Input formats

| Method | Format |
|---|---|
| `Analyze(ReadOnlySpan<float>, int)` | Normalized mono float, `[-1, +1]` |
| `AnalyzePcm16(ReadOnlySpan<byte>, AudioBufferFormat)` | Signed 16-bit PCM, little-endian |
| `AnalyzeFloat32(ReadOnlySpan<byte>, AudioBufferFormat)` | IEEE 754 32-bit float, little-endian |

Multi-channel buffers are mixed to mono according to `ChannelMixMode`:

| Mode | Behavior |
|---|---|
| `FirstChannel` | Use channel 0 |
| `PreferredChannel` | Use `AudioBufferFormat.PreferredChannel` |
| `AverageAllChannels` | Arithmetic mean of all channels |

## Configuration

```csharp
new ExtremaSpectrumOptions
{
    BinCount = 128,
    MinFrequencyHz = 100f,
    MaxFrequencyHz = 8000f,
    MaxPasses = 16,
    MinPeriodSamples = 2,
    MaxPeriodSamples = 0,
    MinAmplitude = 0f,
    AccumulationMode = AccumulationMode.Amplitude
}
```

## Limitations

- Not FFT. Bin values are accumulated oscillation amplitudes or energies, not DFT coefficients.
- There is no windowing, zero-padding, or spectral leakage correction.
- Frequency resolution improves with longer input buffers.
- The greedy left-to-right pass does not resolve overlap inside the same pass.
- Results depend on signal amplitude. Normalize input if absolute comparison matters.

## License

MIT
