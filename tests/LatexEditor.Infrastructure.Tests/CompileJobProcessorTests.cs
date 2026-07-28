using System.Text;
using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;
using LatexEditor.Infrastructure.Compile;
using Microsoft.Extensions.Logging.Abstractions;

namespace LatexEditor.Infrastructure.Tests;

public class CompileJobProcessorTests
{
    private readonly ProjectRepoStub _projectRepo = new();
    private readonly FileRepoStub _fileRepo = new();
    private readonly JobRepoStub _jobRepo = new();
    private readonly FakeStorage _storage = new();
    private readonly FakeCompiler _compiler = new();
    private readonly CompileJobProcessor _processor;

    private readonly Guid _projectId = Guid.NewGuid();

    public CompileJobProcessorTests()
    {
        _projectRepo.Project = new Project { Id = _projectId, Name = "Demo", OwnerId = "owner" };
        _processor = new CompileJobProcessor(
            _projectRepo, _fileRepo, _jobRepo, _storage, _compiler,
            NullLogger<CompileJobProcessor>.Instance);
    }

    private CompileJob SeedJob()
    {
        var job = new CompileJob { Id = Guid.NewGuid(), ProjectId = _projectId };
        _jobRepo.Jobs[job.Id] = job;
        return job;
    }

    private void SeedFile(string path, string content)
    {
        var key = $"{_projectId}/{Guid.NewGuid():N}";
        _fileRepo.Files.Add(new ProjectFile { ProjectId = _projectId, Path = path, StorageKey = key });
        _storage.Objects[key] = Encoding.UTF8.GetBytes(content);
    }

    [Fact]
    public async Task Process_Success_UploadsPdfAndUpdatesJobAndProject()
    {
        var job = SeedJob();
        SeedFile("main.tex", "\\documentclass{article}");
        SeedFile("sections/intro.tex", "intro");
        _compiler.Behavior = FakeCompiler.Succeed;

        await _processor.ProcessAsync(job.Id);

        Assert.Equal(CompileStatus.Success, job.Status);
        Assert.NotNull(job.OutputStorageKey);
        Assert.True(_storage.Objects.ContainsKey(job.OutputStorageKey));
        Assert.Equal("%PDF", Encoding.ASCII.GetString(_storage.Objects[job.OutputStorageKey][..4]));
        Assert.NotNull(job.CompletedAt);
        Assert.NotNull(job.DurationMs);
        Assert.Equal(CompileStatus.Success, _projectRepo.Project.LastCompileStatus);
    }

    [Fact]
    public async Task Process_MissingEntryFile_Fails()
    {
        var job = SeedJob();
        SeedFile("other.tex", "content");

        await _processor.ProcessAsync(job.Id);

        Assert.Equal(CompileStatus.Failed, job.Status);
        Assert.Contains("main.tex", job.ErrorMessage);
    }

    [Fact]
    public async Task Process_CompilerError_FailsWithCapturedOutput()
    {
        var job = SeedJob();
        SeedFile("main.tex", "\\bad");
        _compiler.Behavior = FakeCompiler.Fail;

        await _processor.ProcessAsync(job.Id);

        Assert.Equal(CompileStatus.Failed, job.Status);
        Assert.Equal("compile failed", job.StdErr);
        Assert.Null(job.OutputStorageKey);
        Assert.Equal(CompileStatus.Failed, _projectRepo.Project.LastCompileStatus);
    }

    [Fact]
    public async Task Process_Timeout_Fails()
    {
        var job = SeedJob();
        SeedFile("main.tex", "content");
        _compiler.Behavior = FakeCompiler.TimeOut;

        await _processor.ProcessAsync(job.Id);

        Assert.Equal(CompileStatus.Failed, job.Status);
        Assert.Contains("timed out", job.ErrorMessage);
    }

    [Fact]
    public async Task Process_OutputNotPdf_Fails()
    {
        var job = SeedJob();
        SeedFile("main.tex", "content");
        _compiler.Behavior = FakeCompiler.ProduceGarbage;

        await _processor.ProcessAsync(job.Id);

        Assert.Equal(CompileStatus.Failed, job.Status);
        Assert.Contains("PDF verification", job.ErrorMessage);
    }

    [Fact]
    public async Task Process_UnknownJobId_DoesNothing()
    {
        await _processor.ProcessAsync(Guid.NewGuid());
        Assert.Empty(_storage.Objects);
    }

    [Fact]
    public async Task Process_CleansUpTempDirectory()
    {
        var job = SeedJob();
        SeedFile("main.tex", "content");
        _compiler.Behavior = FakeCompiler.Succeed;

        await _processor.ProcessAsync(job.Id);

        Assert.False(Directory.Exists(Path.Combine(Path.GetTempPath(), $"latex-compile-{job.Id:N}")));
    }

    private sealed class ProjectRepoStub : IProjectRepository
    {
        public Project Project { get; set; } = null!;

        public Task<IReadOnlyList<Project>> GetByOwnerAsync(string ownerId) => throw new NotSupportedException();
        public Task<Project?> GetByIdAsync(Guid id, string ownerId) => throw new NotSupportedException();
        public Task<Project?> GetByIdUnrestrictedAsync(Guid id) => Task.FromResult<Project?>(Project.Id == id ? Project : null);
        public Task AddAsync(Project project) => throw new NotSupportedException();
        public Task UpdateAsync(Project project) { Project = project; return Task.CompletedTask; }
        public Task RemoveAsync(Project project) => throw new NotSupportedException();
    }

    private sealed class FileRepoStub : IProjectFileRepository
    {
        public List<ProjectFile> Files { get; } = [];

        public Task<IReadOnlyList<ProjectFile>> GetByProjectIdAsync(Guid projectId) =>
            Task.FromResult<IReadOnlyList<ProjectFile>>(Files);
        public Task<ProjectFile?> GetByPathAsync(Guid projectId, string path) => throw new NotSupportedException();
        public Task UpsertAsync(ProjectFile file) => throw new NotSupportedException();
        public Task RemoveAsync(Guid projectId, string path) => throw new NotSupportedException();
    }

    private sealed class JobRepoStub : ICompileJobRepository
    {
        public Dictionary<Guid, CompileJob> Jobs { get; } = new();

        public Task<CompileJob?> GetByIdAsync(Guid id) =>
            Task.FromResult(Jobs.GetValueOrDefault(id));
        public Task<IReadOnlyList<CompileJob>> GetByProjectIdAsync(Guid projectId) => throw new NotSupportedException();
        public Task AddAsync(CompileJob job) { Jobs[job.Id] = job; return Task.CompletedTask; }
        public Task UpdateAsync(CompileJob job) => Task.CompletedTask;
    }

    private sealed class FakeStorage : IFileStorage
    {
        public Dictionary<string, byte[]> Objects { get; } = new();

        public StorageProvider Provider => StorageProvider.Local;

        public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            Objects[key] = ms.ToArray();
            return Task.CompletedTask;
        }

        public Task<Stream?> GetAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<Stream?>(Objects.TryGetValue(key, out var b) ? new MemoryStream(b) : null);

        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(Objects.ContainsKey(key));

        public Task DeleteAsync(string key, CancellationToken ct = default) { Objects.Remove(key); return Task.CompletedTask; }

        public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default) =>
            Task.FromResult($"/files/{key}");
    }

    private sealed class FakeCompiler : ITectonicCompiler
    {
        public Func<string, TectonicResult> Behavior { get; set; } = Succeed;

        public static TectonicResult Succeed(string workDir) => Produce(workDir, "%PDF-1.7 fake");

        public static TectonicResult ProduceGarbage(string workDir) => Produce(workDir, "not a pdf");

        public static TectonicResult Fail(string workDir) => new()
        {
            ExitCode = 1,
            StdErr = "compile failed"
        };

        public static TectonicResult TimeOut(string workDir) => new()
        {
            ExitCode = -1,
            TimedOut = true
        };

        private static TectonicResult Produce(string workDir, string content)
        {
            var pdfPath = Path.Combine(workDir, "main.pdf");
            File.WriteAllText(pdfPath, content);
            return new TectonicResult { ExitCode = 0, OutputPdfPath = pdfPath };
        }

        public Task<TectonicResult> CompileAsync(string workingDirectory, string entryFile, CancellationToken ct = default) =>
            Task.FromResult(Behavior(workingDirectory));
    }
}
