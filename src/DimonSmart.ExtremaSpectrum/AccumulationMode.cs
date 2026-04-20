namespace DimonSmart.ExtremaSpectrum;

/// <summary>
/// Controls what quantity is accumulated into each frequency bin.
/// </summary>
public enum AccumulationMode
{
    /// <summary>Accumulate the amplitude of each detected oscillation.</summary>
    Amplitude,

    /// <summary>Accumulate the squared amplitude (energy) of each detected oscillation.</summary>
    Energy
}
