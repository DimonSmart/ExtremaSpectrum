namespace DimonSmart.ExtremaSpectrum;

/// <summary>
/// Determines how a multi-channel buffer is reduced to a mono signal for analysis.
/// </summary>
public enum ChannelMixMode
{
    /// <summary>Use only the first channel (index 0).</summary>
    FirstChannel,

    /// <summary>Use only the channel at <see cref="AudioBufferFormat.PreferredChannel"/>.</summary>
    PreferredChannel,

    /// <summary>Use the arithmetic mean of all channels.</summary>
    AverageAllChannels
}
