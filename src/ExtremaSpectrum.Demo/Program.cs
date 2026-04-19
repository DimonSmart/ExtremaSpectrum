using NAudio.Wave;
using Spectre.Console;
using System.Globalization;

namespace ExtremaSpectrum.Demo;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var options = ParseArguments(args);
            if (options.ListInputDevices)
            {
                PrintInputDevices();
                return 0;
            }

            if (options.UseMicrophone)
                return RunMicrophone(options);

            var waveFile = WaveFileReader.ReadMono(options.InputPath);
            if (options.MinFrequencyHz >= waveFile.NyquistHz)
                throw new ArgumentException(
                    $"Min frequency must be below Nyquist ({waveFile.NyquistHz.ToString("F1", CultureInfo.InvariantCulture)} Hz).");

            var analysisOptions = CreateAnalysisOptions(options, waveFile.SampleRate);
            var analyzer = new ExtremaSpectrumAnalyzer(analysisOptions);
            var segments = AnalyzeSegments(waveFile, analyzer, options);
            SpectrumConsoleRenderer.Render(options, waveFile, segments);
            ExportStepImagesIfRequested(options, waveFile, segments);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Ошибка:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private static IReadOnlyList<SpectrumSegment> AnalyzeSegments(
        WaveFile waveFile,
        ExtremaSpectrumAnalyzer analyzer,
        SegmentedSpectrumOptions options)
    {
        var windowSamples = options.WindowSamples(waveFile.SampleRate);
        var hopSamples = options.HopSamples(waveFile.SampleRate);
        var segments = new List<SpectrumSegment>();
        var requiresDetailedReport = options.DumpPasses || options.StepImageOutputDirectory is not null;

        for (var startSample = 0; startSample < waveFile.FrameCount; startSample += hopSamples)
        {
            var sampleCount = Math.Min(windowSamples, waveFile.FrameCount - startSample);
            if (sampleCount <= 0)
                break;

            var segmentSamples = waveFile.Samples.AsSpan(startSample, sampleCount);
            var analysisReport = requiresDetailedReport
                ? analyzer.AnalyzeDetailed(segmentSamples, waveFile.SampleRate)
                : null;

            var result = analysisReport is null
                ? analyzer.Analyze(segmentSamples, waveFile.SampleRate)
                : AnalysisResultFactory.FromAnalysisReport(analysisReport);

            segments.Add(new SpectrumSegment
            {
                Index = segments.Count,
                StartSample = startSample,
                SampleCount = sampleCount,
                Result = result,
                DetailedReport = analysisReport
            });
        }

        return segments;
    }

    private static SegmentedSpectrumOptions ParseArguments(string[] args)
    {
        var inputPath = FindDefaultInputPath();
        var useMicrophone = false;
        var listInputDevices = false;
        var microphoneDeviceIndex = 0;
        var microphoneSampleRate = 16000;
        var microphoneBufferMilliseconds = 50;
        float microphoneSilenceRmsThreshold = 0.0005f;
        float microphoneDisplayReferenceRms = 0.01f;
        var accumulationMode = AccumulationMode.Amplitude;
        float minFrequencyHz = 100f;
        float minAmplitude = 0f;
        double windowSeconds = 5d;
        double overlapSeconds = 1d;
        var binCount = 20;
        var chartHeight = 12;
        var maxPasses = 12;
        var dumpPasses = false;
        string? stepImageOutputDirectory = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;

                case "--input":
                case "-i":
                    inputPath = RequireValue(args, ref i, arg);
                    break;

                case "--microphone":
                case "--mic":
                    useMicrophone = true;
                    break;

                case "--list-input-devices":
                case "--list-microphones":
                    listInputDevices = true;
                    break;

                case "--device-index":
                    microphoneDeviceIndex = ParseNonNegativeInt(RequireValue(args, ref i, arg), arg);
                    break;

                case "--microphone-sample-rate":
                    microphoneSampleRate = ParsePositiveInt(RequireValue(args, ref i, arg), arg);
                    break;

                case "--buffer-milliseconds":
                    microphoneBufferMilliseconds = ParsePositiveInt(RequireValue(args, ref i, arg), arg);
                    break;

                case "--silence-rms-threshold":
                    microphoneSilenceRmsThreshold = ParseNonNegativeFloat(RequireValue(args, ref i, arg), arg);
                    break;

                case "--display-reference-rms":
                    microphoneDisplayReferenceRms = ParsePositiveFloat(RequireValue(args, ref i, arg), arg);
                    break;

                case "--min-frequency":
                    minFrequencyHz = ParseNonNegativeFloat(RequireValue(args, ref i, arg), arg);
                    break;

                case "--min-amplitude":
                    minAmplitude = ParseNonNegativeFloat(RequireValue(args, ref i, arg), arg);
                    break;

                case "--accumulation":
                    accumulationMode = AccumulationModeCli.Parse(RequireValue(args, ref i, arg));
                    break;

                case "--window-seconds":
                    windowSeconds = ParsePositiveDouble(RequireValue(args, ref i, arg), arg);
                    break;

                case "--overlap-seconds":
                    overlapSeconds = ParseNonNegativeDouble(RequireValue(args, ref i, arg), arg);
                    break;

                case "--bins":
                    binCount = ParsePositiveInt(RequireValue(args, ref i, arg), arg);
                    break;

                case "--height":
                    chartHeight = ParsePositiveInt(RequireValue(args, ref i, arg), arg);
                    break;

                case "--passes":
                    maxPasses = ParsePositiveInt(RequireValue(args, ref i, arg), arg);
                    break;

                case "--dump-passes":
                    dumpPasses = true;
                    break;

                case "--export-step-images":
                    stepImageOutputDirectory = Path.GetFullPath(RequireValue(args, ref i, arg));
                    break;

                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                        throw new ArgumentException($"Unknown argument '{arg}'. Use --help for usage.");

                    inputPath = arg;
                    break;
            }
        }

        if (overlapSeconds >= windowSeconds)
            throw new ArgumentException("Overlap must be smaller than the window size.");
        if (useMicrophone && stepImageOutputDirectory is not null)
            throw new ArgumentException("--export-step-images is supported only for file input.");

        return new SegmentedSpectrumOptions
        {
            InputPath = Path.GetFullPath(inputPath),
            UseMicrophone = useMicrophone,
            ListInputDevices = listInputDevices,
            MicrophoneDeviceIndex = microphoneDeviceIndex,
            MicrophoneSampleRate = microphoneSampleRate,
            MicrophoneBufferMilliseconds = microphoneBufferMilliseconds,
            MicrophoneSilenceRmsThreshold = microphoneSilenceRmsThreshold,
            MicrophoneDisplayReferenceRms = microphoneDisplayReferenceRms,
            AccumulationMode = accumulationMode,
            MinFrequencyHz = minFrequencyHz,
            MinAmplitude = minAmplitude,
            WindowSeconds = windowSeconds,
            OverlapSeconds = overlapSeconds,
            BinCount = binCount,
            ChartHeight = chartHeight,
            MaxPasses = maxPasses,
            DumpPasses = dumpPasses,
            StepImageOutputDirectory = stepImageOutputDirectory
        };
    }

    private static string FindDefaultInputPath()
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "data", "demo-note.wav");
            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        return Path.Combine(Environment.CurrentDirectory, "data", "demo-note.wav");
    }

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        index++;
        if (index >= args.Length)
            throw new ArgumentException($"Missing value after '{optionName}'.");

        return args[index];
    }

    private static int ParsePositiveInt(string value, string optionName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
            throw new ArgumentException($"'{optionName}' expects a positive integer.");

        return parsed;
    }

    private static double ParsePositiveDouble(string value, string optionName)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0d)
            throw new ArgumentException($"'{optionName}' expects a positive number.");

        return parsed;
    }

    private static double ParseNonNegativeDouble(string value, string optionName)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || parsed < 0d)
            throw new ArgumentException($"'{optionName}' expects a non-negative number.");

        return parsed;
    }

    private static int ParseNonNegativeInt(string value, string optionName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
            throw new ArgumentException($"'{optionName}' expects a non-negative integer.");

        return parsed;
    }

    private static float ParseNonNegativeFloat(string value, string optionName)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || parsed < 0f)
            throw new ArgumentException($"'{optionName}' expects a non-negative number.");

        return parsed;
    }

    private static float ParsePositiveFloat(string value, string optionName)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0f)
            throw new ArgumentException($"'{optionName}' expects a positive number.");

        return parsed;
    }

    private static void PrintHelp()
    {
        AnsiConsole.MarkupLine("[bold]ExtremaSpectrum.Demo[/]");
        AnsiConsole.MarkupLine("[grey]Analyzes a WAV file in overlapping windows and prints equalizer-like histograms.[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Usage:");
        AnsiConsole.WriteLine("  dotnet run --project src/ExtremaSpectrum.Demo -- [--input PATH] [--microphone] [--list-input-devices] [--device-index N] [--microphone-sample-rate HZ] [--buffer-milliseconds N] [--silence-rms-threshold VALUE] [--display-reference-rms VALUE] [--accumulation NAME] [--min-frequency HZ] [--min-amplitude VALUE] [--window-seconds N] [--overlap-seconds N] [--bins N] [--height N] [--passes N] [--dump-passes] [--export-step-images DIR]");
        AnsiConsole.WriteLine("  accumulation: amplitude, energy");
        AnsiConsole.WriteLine("  microphone: use --microphone to capture live audio, Ctrl+C to stop");
        AnsiConsole.WriteLine("  silence gate: --silence-rms-threshold 0 disables it");
        AnsiConsole.WriteLine("  live display scaling: --display-reference-rms 0.01 maps that RMS to full chart height");
        AnsiConsole.WriteLine("  export-step-images: writes SVG files showing the waveform before and after each pass");
    }

    private static ExtremaSpectrumOptions CreateAnalysisOptions(
        SegmentedSpectrumOptions options,
        int sampleRate)
    {
        return new ExtremaSpectrumOptions
        {
            BinCount = options.BinCount,
            MinFrequencyHz = options.MinFrequencyHz,
            MaxFrequencyHz = sampleRate / 2f,
            MaxPasses = options.MaxPasses,
            MinAmplitude = options.MinAmplitude,
            AccumulationMode = options.AccumulationMode
        };
    }

    private static int RunMicrophone(SegmentedSpectrumOptions options)
    {
        if (WaveInEvent.DeviceCount <= 0)
            throw new InvalidOperationException("No microphone input devices were found.");
        if (options.MicrophoneDeviceIndex >= WaveInEvent.DeviceCount)
            throw new ArgumentOutOfRangeException(
                nameof(options.MicrophoneDeviceIndex),
                $"Device index {options.MicrophoneDeviceIndex} is out of range. Use --list-input-devices.");

        var capabilities = WaveInEvent.GetCapabilities(options.MicrophoneDeviceIndex);
        var analysisOptions = CreateAnalysisOptions(options, options.MicrophoneSampleRate);
        if (options.MinFrequencyHz >= analysisOptions.MaxFrequencyHz)
            throw new ArgumentException(
                $"Min frequency must be below Nyquist ({analysisOptions.MaxFrequencyHz.ToString("F1", CultureInfo.InvariantCulture)} Hz).");

        var liveAnalyzer = new LiveSpectrumAnalyzer(
            analysisOptions,
            options.WindowSamples(options.MicrophoneSampleRate),
            options.HopSamples(options.MicrophoneSampleRate));

        var bufferFormat = new AudioBufferFormat
        {
            SampleRate = options.MicrophoneSampleRate,
            Channels = 1,
            BitsPerSample = 16,
            Interleaved = true,
            ChannelMixMode = ChannelMixMode.FirstChannel
        };

        using var cts = new CancellationTokenSource();
        var sync = new object();
        LiveSpectrumFrame? latestFrame = null;
        string statusText = "Нажмите Ctrl+C для остановки.";
        string? errorText = null;

        using var waveIn = new WaveInEvent
        {
            DeviceNumber = options.MicrophoneDeviceIndex,
            WaveFormat = new WaveFormat(options.MicrophoneSampleRate, 16, 1),
            BufferMilliseconds = options.MicrophoneBufferMilliseconds
        };

        waveIn.DataAvailable += (_, eventArgs) =>
        {
            try
            {
                if (!liveAnalyzer.PushPcm16(
                    new ReadOnlySpan<byte>(eventArgs.Buffer, 0, eventArgs.BytesRecorded),
                    bufferFormat,
                    out var frame))
                {
                    return;
                }

                frame = MicrophoneSilenceGate.Apply(frame!, options.MicrophoneSilenceRmsThreshold);

                lock (sync)
                {
                    latestFrame = frame;
                }
            }
            catch (Exception ex)
            {
                lock (sync)
                {
                    errorText = ex.Message;
                    statusText = "Ошибка во время анализа.";
                }

                cts.Cancel();
            }
        };

        waveIn.RecordingStopped += (_, eventArgs) =>
        {
            if (eventArgs.Exception is not null)
            {
                lock (sync)
                {
                    errorText = eventArgs.Exception.Message;
                    statusText = "Запись остановлена с ошибкой.";
                }
            }

            cts.Cancel();
        };

        ConsoleCancelEventHandler? cancelHandler = null;
        cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            lock (sync)
                statusText = "Остановка...";
            cts.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            waveIn.StartRecording();

            var deviceName = $"{options.MicrophoneDeviceIndex}: {capabilities.ProductName}";
            AnsiConsole.Live(MicrophoneConsoleRenderer.Build(options, deviceName, frame: null, statusText))
                .Overflow(VerticalOverflow.Crop)
                .Cropping(VerticalOverflowCropping.Top)
                .Start(context =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        LiveSpectrumFrame? frame;
                        string currentStatus;
                        string? currentError;

                        lock (sync)
                        {
                            frame = latestFrame;
                            currentStatus = statusText;
                            currentError = errorText;
                        }

                        context.UpdateTarget(MicrophoneConsoleRenderer.Build(options, deviceName, frame, currentStatus));
                        context.Refresh();

                        if (currentError is not null)
                            break;

                        Thread.Sleep(60);
                    }
                });
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            try
            {
                waveIn.StopRecording();
            }
            catch
            {
                // Ignore stop errors during shutdown.
            }
        }

        if (errorText is not null)
            throw new InvalidOperationException(errorText);

        return 0;
    }

    private static void ExportStepImagesIfRequested(
        SegmentedSpectrumOptions options,
        WaveFile waveFile,
        IReadOnlyList<SpectrumSegment> segments)
    {
        if (options.StepImageOutputDirectory is null)
            return;

        var exportedFiles = WaveformStepSvgExporter.Export(
            options.StepImageOutputDirectory,
            options.InputPath,
            waveFile,
            segments);

        AnsiConsole.MarkupLine(
            $"[grey]SVG steps exported:[/] {exportedFiles.Count} -> {Markup.Escape(options.StepImageOutputDirectory)}");
    }

    private static void PrintInputDevices()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[grey]Index[/]")
            .AddColumn("[grey]Устройство[/]")
            .AddColumn("[grey]Каналы[/]");

        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var capabilities = WaveInEvent.GetCapabilities(i);
            table.AddRow(
                i.ToString(CultureInfo.InvariantCulture),
                Markup.Escape(capabilities.ProductName),
                capabilities.Channels.ToString(CultureInfo.InvariantCulture));
        }

        if (WaveInEvent.DeviceCount == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Устройства ввода не найдены.[/]");
            return;
        }

        AnsiConsole.Write(table);
    }
}
