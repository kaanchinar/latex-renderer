using System.Diagnostics;
using LatexEditor.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace LatexEditor.Infrastructure.Compile;

/// <summary>
/// <see cref="ITectonicCompiler"/> that shells out to the Tectonic binary.
/// Shell escape is never enabled (Tectonic keeps it off unless explicitly
/// requested via a flag this wrapper never passes). The process is killed
/// when the configured timeout elapses or cancellation is requested.
/// </summary>
public class TectonicCompiler(IOptions<TectonicOptions> options) : ITectonicCompiler
{
    private readonly TectonicOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<TectonicResult> CompileAsync(string workingDirectory, string entryFile, CancellationToken ct = default)
    {
        var outputPdfPath = Path.Combine(workingDirectory,
            Path.ChangeExtension(Path.GetFileName(entryFile), ".pdf"));

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(entryFile);
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(".");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        using var process = new Process();
        process.StartInfo = startInfo;
        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stdErrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = !ct.IsCancellationRequested;
            try
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (InvalidOperationException) { }
            if (ct.IsCancellationRequested) throw;
        }

        var stdOut = await SafeRead(stdOutTask);
        var stdErr = await SafeRead(stdErrTask);
        var exitCode = process.HasExited ? process.ExitCode : -1;

        string? pdfPath = null;
        if (!timedOut && exitCode == 0 && File.Exists(outputPdfPath))
            pdfPath = outputPdfPath;

        return new TectonicResult
        {
            ExitCode = exitCode,
            TimedOut = timedOut,
            StdOut = stdOut,
            StdErr = stdErr,
            OutputPdfPath = pdfPath
        };
    }

    private static async Task<string> SafeRead(Task<string> readTask)
    {
        try { return await readTask; }
        catch (OperationCanceledException) { return string.Empty; }
    }
}
