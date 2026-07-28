namespace LatexEditor.Core.Interfaces;

/// <summary>Outcome of a single Tectonic invocation.</summary>
public class TectonicResult
{
    /// <summary>Process exit code. 0 indicates a successful compile.</summary>
    public int ExitCode { get; init; }

    /// <summary>Whether the process was killed because it exceeded the configured timeout.</summary>
    public bool TimedOut { get; init; }

    /// <summary>Captured standard output.</summary>
    public string StdOut { get; init; } = string.Empty;

    /// <summary>Captured standard error.</summary>
    public string StdErr { get; init; } = string.Empty;

    /// <summary>
    /// Full path to the generated PDF. Set only when the compile succeeded
    /// (exit code 0 and the output file exists).
    /// </summary>
    public string? OutputPdfPath { get; init; }
}

/// <summary>
/// Runs the Tectonic LaTeX engine against a directory of source files.
/// Implementations must never enable shell escape.
/// </summary>
public interface ITectonicCompiler
{
    /// <summary>
    /// Compiles <paramref name="entryFile"/> inside <paramref name="workingDirectory"/>.
    /// </summary>
    /// <param name="workingDirectory">Directory containing the project source files.</param>
    /// <param name="entryFile">Entry document path relative to <paramref name="workingDirectory"/> (e.g. <c>main.tex</c>).</param>
    /// <param name="ct">Cancellation token; killing the process on cancellation.</param>
    Task<TectonicResult> CompileAsync(string workingDirectory, string entryFile, CancellationToken ct = default);
}
