namespace LatexEditor.Infrastructure.Compile;

/// <summary>
/// Configuration for the Tectonic compiler, bound from the <c>Tectonic</c>
/// configuration section (or <c>Tectonic__*</c> environment variables).
/// </summary>
public class TectonicOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Tectonic";

    /// <summary>Path or name of the Tectonic executable.</summary>
    public string ExecutablePath { get; set; } = "tectonic";

    /// <summary>Hard timeout for a single compile, in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 60;
}
