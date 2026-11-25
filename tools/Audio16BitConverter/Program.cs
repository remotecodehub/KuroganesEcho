using FFMpegCore; 
using FFMpegCore.Extensions.Downloader;
using FFMpegCore.Extensions.Downloader.Enums;

// Define uma pasta local para guardar os binarios (ex.: junto ao executavel)
string ffmpegDir = Path.Combine(AppContext.BaseDirectory, "ffmpeg");
Directory.CreateDirectory(ffmpegDir);

GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegDir);

// Baixa os binarios automaticamente apenas se ainda nao existirem na pasta
string ffmpegExe = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
if (!File.Exists(Path.Combine(ffmpegDir, ffmpegExe)))
{
    Console.WriteLine("Baixando binarios do FFmpeg...");
    await FFMpegDownloader.DownloadBinaries(FFMpegVersions.LatestAvailable, FFMpegBinaries.FFMpeg | FFMpegBinaries.FFProbe);
}

if (args.Length == 0)
{
    Console.WriteLine("Uso: dotnet run -- <arquivo_entrada> [arquivo_saida.wav] [sample_rate]");
    Console.WriteLine("Exemplo: dotnet run -- musica_24bit.flac musica_16bit.wav 44100");
    return 1;
}

string inputPath = Path.GetFullPath(args[0]);

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Erro: arquivo nao encontrado -> {inputPath}");
    return 1;
}

string outputPath = args.Length >= 2
    ? Path.GetFullPath(args[1])
    : Path.Combine(
        Path.GetDirectoryName(inputPath)!,
        $"{Path.GetFileNameWithoutExtension(inputPath)}_16bit.wav");

int? targetSampleRate = args.Length >= 3 && int.TryParse(args[2], out int sr) ? sr : null;

try
{
    Console.WriteLine($"Analisando: {Path.GetFileName(inputPath)}");
    var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
    var audioStream = mediaInfo.PrimaryAudioStream;

    if (audioStream is null)
    {
        Console.Error.WriteLine("Erro: nenhuma faixa de audio encontrada no arquivo.");
        return 1;
    }

    int sampleRate = targetSampleRate ?? audioStream.SampleRateHz;

    Console.WriteLine($"  Codec original : {audioStream.CodecName}");
    Console.WriteLine($"  Sample rate    : {audioStream.SampleRateHz} Hz -> {sampleRate} Hz");
    Console.WriteLine($"  Canais         : {audioStream.Channels}");
    Console.WriteLine($"  Duracao        : {audioStream.Duration:hh\\:mm\\:ss\\.fff}");
    Console.WriteLine();
    Console.WriteLine("Convertendo para 16-bit PCM (pcm_s16le) com dithering triangular...");

    await FFMpegArguments
        .FromFileInput(inputPath)
        .OutputToFile(outputPath, overwrite: true, options => options
            .WithAudioCodec("pcm_s16le")
            .WithAudioSamplingRate(sampleRate)
            .WithCustomArgument("-af aresample=dither_method=triangular")
            .DisableChannel(FFMpegCore.Enums.Channel.Video))
        .ProcessAsynchronously();

    Console.WriteLine($"Concluido: {outputPath}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Falha na conversao: {ex.Message}");
    return 1;
}