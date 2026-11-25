using FFMpegCore; 
using FFMpegCore.Extensions.Downloader;
using FFMpegCore.Extensions.Downloader.Enums;

if (args.Length == 0)
{
    Console.WriteLine("Uso:");
    Console.WriteLine("  Arquivo   : dotnet run -- <arquivo> [saida.wav] [sample_rate]");
    Console.WriteLine("  Diretorio : dotnet run -- <diretorio> [sample_rate]");
    Console.WriteLine();
    Console.WriteLine("Exemplos:");
    Console.WriteLine("  dotnet run -- musica_24bit.flac musica_16bit.wav 44100");
    Console.WriteLine("  dotnet run -- ./meus_audios 44100");
    return 1;
}

string inputPath = Path.GetFullPath(args[0]);

if (!File.Exists(inputPath) && !Directory.Exists(inputPath))
{
    Console.Error.WriteLine($"Erro: arquivo ou diretorio nao encontrado -> {inputPath}");
    return 1;
}

// Configura e baixa os binarios do FFmpeg localmente se necessario
string ffmpegDir = Path.Combine(AppContext.BaseDirectory, "ffmpeg");
Directory.CreateDirectory(ffmpegDir);
GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegDir);

string ffmpegExe = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
string ffprobeExe = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";

if (!File.Exists(Path.Combine(ffmpegDir, ffmpegExe)) || !File.Exists(Path.Combine(ffmpegDir, ffprobeExe)))
{
    Console.WriteLine("Baixando binarios do FFmpeg/FFProbe...");
    await FFMpegDownloader.DownloadBinariesAsync(
        FFMpegVersions.LatestAvailable,
        FFMpegBinaries.FFMpeg | FFMpegBinaries.FFProbe);
    Console.WriteLine("Download concluido.\n");
}
// Modo Diretorio
if (Directory.Exists(inputPath))
{
    int? targetSampleRate = args.Length >= 2 && int.TryParse(args[1], out int sr) ? sr : null;
    string convertedDir = Path.Combine(inputPath, "Converted");
    Directory.CreateDirectory(convertedDir);

    string[] files = Directory.GetFiles(inputPath, "*", SearchOption.TopDirectoryOnly);

    if (files.Length == 0)
    {
        Console.WriteLine("Nenhum arquivo encontrado no diretorio.");
        return 0;
    }

    Console.WriteLine($"Diretorio : {inputPath}");
    Console.WriteLine($"Saida     : {convertedDir}");
    Console.WriteLine($"Arquivos  : {files.Length}\n");

    int converted = 0, skipped = 0, failed = 0;

    foreach (string file in files)
    {
        string outputFile = Path.Combine(
            convertedDir,
            $"{Path.GetFileNameWithoutExtension(file)}.wav");

        var status = await ConvertFileAsync(file, outputFile, targetSampleRate);

        switch (status)
        {
            case ConversionStatus.Success: converted++; break;
            case ConversionStatus.Skipped: skipped++; break;
            case ConversionStatus.Failed:  failed++;  break;
        }
    }

    Console.WriteLine("----------------------------------------");
    Console.WriteLine($"Finalizado | Convertidos: {converted} | Ignorados: {skipped} | Erros: {failed}");
    return failed > 0 ? 1 : 0;
}

// Modo Arquivo Unico
{
    string outputPath = args.Length >= 2 && !int.TryParse(args[1], out _)
        ? Path.GetFullPath(args[1])
        : Path.Combine(
            Path.GetDirectoryName(inputPath)!,
            $"{Path.GetFileNameWithoutExtension(inputPath)}_16bit.wav");

    int? targetSampleRate = args.Length >= 3 && int.TryParse(args[2], out int sr3)
        ? sr3
        : (args.Length == 2 && int.TryParse(args[1], out int sr2) ? sr2 : null);

    var status = await ConvertFileAsync(inputPath, outputPath, targetSampleRate);
    return status == ConversionStatus.Success ? 0 : 1;
}

static async Task<ConversionStatus> ConvertFileAsync(string inputFile, string outputFile, int? targetSampleRate)
{
    string fileName = Path.GetFileName(inputFile);

    try
    {
        IMediaAnalysis mediaInfo;
        try
        {
            mediaInfo = await FFProbe.AnalyseAsync(inputFile);
        }
        catch
        {
            Console.WriteLine($"[Ignorado] {fileName} (nao e um arquivo de midia valido)");
            return ConversionStatus.Skipped;
        }

        var audioStream = mediaInfo.PrimaryAudioStream;
        if (audioStream is null)
        {
            Console.WriteLine($"[Ignorado] {fileName} (sem faixa de audio)");
            return ConversionStatus.Skipped;
        }

        int? resolvedRate = targetSampleRate ?? audioStream.SampleRateHz;
        int sampleRate = resolvedRate is > 0 ? resolvedRate.Value : 44100;

        Console.WriteLine($"[Convertendo] {fileName}");
        Console.WriteLine($"  {audioStream.CodecName} | {audioStream.SampleRateHz} Hz -> {sampleRate} Hz | {audioStream.Channels}ch | {audioStream.Duration:hh\\:mm\\:ss\\.fff}");

        await FFMpegArguments
            .FromFileInput(inputFile)
            .OutputToFile(outputFile, overwrite: true, options => options
                .WithAudioCodec("pcm_s16le")
                .WithAudioSamplingRate(sampleRate)
                .WithCustomArgument("-vn -af aresample=dither_method=triangular"))
            .ProcessAsynchronously();

        Console.WriteLine($"  -> Salvo em: {outputFile}\n");
        return ConversionStatus.Success;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[Erro] {fileName}: {ex.Message}\n");
        return ConversionStatus.Failed;
    }
}

enum ConversionStatus
{
    Success,
    Skipped,
    Failed
}