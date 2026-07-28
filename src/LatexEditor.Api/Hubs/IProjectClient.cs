namespace LatexEditor.Api.Hubs;

/// <summary>
/// Client-side contract for the project hub. The server calls these methods
/// on every client that joined a project's group.
/// </summary>
public interface IProjectClient
{
    /// <summary>A compile job started running.</summary>
    Task CompileStarted(Guid jobId);

    /// <summary>A compile job succeeded; the PDF is downloadable at the given URL.</summary>
    Task CompileCompleted(Guid jobId, string pdfUrl);

    /// <summary>A compile job failed or was cancelled.</summary>
    Task CompileFailed(Guid jobId, string error);

    /// <summary>A line of compiler output arrived.</summary>
    Task CompileOutput(string stdoutLine);
}
