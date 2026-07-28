using LatexEditor.Infrastructure.Compile;
using Microsoft.Extensions.Options;

namespace LatexEditor.Infrastructure.Tests;

public class TectonicCompilerTests : IDisposable
{
    private readonly string _workDir;

    public TectonicCompilerTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), $"tectonic-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true);
    }

    private string WriteScript(string name, string body)
    {
        var path = Path.Combine(_workDir, name);
        File.WriteAllText(path, $"#!/bin/sh\n{body}\n");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead);
        return path;
    }

    private TectonicCompiler CreateCompiler(string executablePath, int timeoutSeconds = 60)
    {
        return new TectonicCompiler(Options.Create(new TectonicOptions
        {
            ExecutablePath = executablePath,
            TimeoutSeconds = timeoutSeconds
        }));
    }

    [Fact]
    public async Task Compile_Success_ReturnsPdfPath()
    {
        var script = WriteScript("fake-tectonic.sh",
            "echo 'compile ok'; printf '%%PDF-fake' > main.pdf");

        var result = await CreateCompiler(script).CompileAsync(_workDir, "main.tex");

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.NotNull(result.OutputPdfPath);
        Assert.True(File.Exists(result.OutputPdfPath));
    }

    [Fact]
    public async Task Compile_NonZeroExit_ReturnsNoPdfPathAndCapturesOutput()
    {
        var script = WriteScript("fake-tectonic.sh",
            "echo 'some output'; echo 'error: undefined control sequence' >&2; exit 1");

        var result = await CreateCompiler(script).CompileAsync(_workDir, "main.tex");

        Assert.Equal(1, result.ExitCode);
        Assert.Null(result.OutputPdfPath);
        Assert.Contains("some output", result.StdOut);
        Assert.Contains("undefined control sequence", result.StdErr);
    }

    [Fact]
    public async Task Compile_ExceedsTimeout_KillsProcessAndReportsTimeout()
    {
        var script = WriteScript("fake-tectonic.sh", "sleep 30");

        var result = await CreateCompiler(script, timeoutSeconds: 1).CompileAsync(_workDir, "main.tex");

        Assert.True(result.TimedOut);
        Assert.Null(result.OutputPdfPath);
    }

    [Fact]
    public async Task Compile_ExitZeroButNoPdf_ReturnsNoPdfPath()
    {
        var script = WriteScript("fake-tectonic.sh", "echo 'no output produced'; exit 0");

        var result = await CreateCompiler(script).CompileAsync(_workDir, "main.tex");

        Assert.Equal(0, result.ExitCode);
        Assert.Null(result.OutputPdfPath);
    }
}
