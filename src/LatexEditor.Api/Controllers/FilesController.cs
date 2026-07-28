using LatexEditor.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LatexEditor.Api.Controllers;

/// <summary>
/// Streams objects out of storage for the local-disk provider, whose
/// "presigned URLs" are server-relative routes handled here.
/// S3 deployments use real presigned URLs and never hit this endpoint.
/// Keys contain non-guessable IDs; treat them as bearer tokens.
/// </summary>
[ApiController]
[Authorize]
[Route("files")]
public class FilesController(IFileStorage storage) : ControllerBase
{
    /// <summary>Download a stored object</summary>
    /// <remarks>Returns 404 if the key does not exist.</remarks>
    [HttpGet("{*key}")]
    public async Task<IActionResult> Get(string key)
    {
        var stream = await storage.GetAsync(key, HttpContext.RequestAborted);
        if (stream is null) return NotFound();

        var contentType = key.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "application/octet-stream";
        return File(stream, contentType, enableRangeProcessing: true);
    }
}
