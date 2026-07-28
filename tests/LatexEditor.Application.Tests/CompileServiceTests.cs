using LatexEditor.Application.Services;
using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;

namespace LatexEditor.Application.Tests;

public class CompileServiceTests
{
    private const string OwnerId = "owner-1";
    private const string OtherUserId = "owner-2";

    private readonly ProjectRepoStub _projectRepo = new();
    private readonly JobRepoStub _jobRepo = new();
    private readonly FakeQueue _queue = new();
    private readonly FakeStorage _storage = new();
    private readonly CompileService _service;

    private readonly Guid _projectId = Guid.NewGuid();

    public CompileServiceTests()
    {
        _projectRepo.Projects.Add(new Project { Id = _projectId, Name = "Demo", OwnerId = OwnerId });
        _service = new CompileService(_projectRepo, _jobRepo, _queue, _storage);
    }

    [Fact]
    public async Task TriggerCompile_CreatesQueuedJobAndEnqueues()
    {
        var job = await _service.TriggerCompileAsync(_projectId, OwnerId);

        Assert.NotNull(job);
        Assert.Equal("Queued", job.Status);
        Assert.False(job.HasOutput);

        var stored = Assert.Single(_jobRepo.Jobs.Values);
        Assert.Equal(job.Id, stored.Id);
        Assert.Equal(_projectId, stored.ProjectId);

        var enqueued = Assert.Single(_queue.Enqueued);
        Assert.Equal(job.Id, enqueued);
    }

    [Fact]
    public async Task TriggerCompile_WrongOwner_ReturnsNullAndDoesNotEnqueue()
    {
        var job = await _service.TriggerCompileAsync(_projectId, OtherUserId);

        Assert.Null(job);
        Assert.Empty(_jobRepo.Jobs);
        Assert.Empty(_queue.Enqueued);
    }

    [Fact]
    public async Task GetJobs_ReturnsJobsForOwner()
    {
        await _service.TriggerCompileAsync(_projectId, OwnerId);

        var jobs = await _service.GetJobsAsync(_projectId, OwnerId);

        Assert.Single(jobs);
    }

    [Fact]
    public async Task GetJobs_WrongOwner_ReturnsEmpty()
    {
        await _service.TriggerCompileAsync(_projectId, OwnerId);

        Assert.Empty(await _service.GetJobsAsync(_projectId, OtherUserId));
    }

    [Fact]
    public async Task GetJobPdfUrl_SucceededJob_ReturnsUrl()
    {
        var job = await _service.TriggerCompileAsync(_projectId, OwnerId);
        var stored = _jobRepo.Jobs[job!.Id];
        stored.Status = CompileStatus.Success;
        stored.OutputStorageKey = $"{_projectId}/jobs/{job.Id}/output.pdf";

        var url = await _service.GetJobPdfUrlAsync(_projectId, job.Id, OwnerId);

        Assert.NotNull(url);
        Assert.Contains(stored.OutputStorageKey, url);
    }

    [Fact]
    public async Task GetJobPdfUrl_JobWithoutOutput_ReturnsNull()
    {
        var job = await _service.TriggerCompileAsync(_projectId, OwnerId);

        Assert.Null(await _service.GetJobPdfUrlAsync(_projectId, job!.Id, OwnerId));
    }

    [Fact]
    public async Task GetJobPdfUrl_WrongOwner_ReturnsNull()
    {
        var job = await _service.TriggerCompileAsync(_projectId, OwnerId);
        _jobRepo.Jobs[job!.Id].OutputStorageKey = "some/key";

        Assert.Null(await _service.GetJobPdfUrlAsync(_projectId, job.Id, OtherUserId));
    }

    [Fact]
    public async Task GetJobPdfUrl_JobFromOtherProject_ReturnsNull()
    {
        var otherProjectId = Guid.NewGuid();
        var job = await _service.TriggerCompileAsync(_projectId, OwnerId);
        _jobRepo.Jobs[job!.Id].OutputStorageKey = "some/key";

        Assert.Null(await _service.GetJobPdfUrlAsync(otherProjectId, job.Id, OwnerId));
    }

    private sealed class ProjectRepoStub : IProjectRepository
    {
        public List<Project> Projects { get; } = [];

        public Task<IReadOnlyList<Project>> GetByOwnerAsync(string ownerId) =>
            Task.FromResult<IReadOnlyList<Project>>(Projects.Where(p => p.OwnerId == ownerId).ToList());
        public Task<Project?> GetByIdAsync(Guid id, string ownerId) =>
            Task.FromResult(Projects.FirstOrDefault(p => p.Id == id && p.OwnerId == ownerId));
        public Task<Project?> GetByIdUnrestrictedAsync(Guid id) =>
            Task.FromResult(Projects.FirstOrDefault(p => p.Id == id));
        public Task AddAsync(Project project) { Projects.Add(project); return Task.CompletedTask; }
        public Task UpdateAsync(Project project) => Task.CompletedTask;
        public Task RemoveAsync(Project project) { Projects.Remove(project); return Task.CompletedTask; }
    }

    private sealed class JobRepoStub : ICompileJobRepository
    {
        public Dictionary<Guid, CompileJob> Jobs { get; } = new();

        public Task<CompileJob?> GetByIdAsync(Guid id) =>
            Task.FromResult(Jobs.TryGetValue(id, out var job) ? job : null);
        public Task<IReadOnlyList<CompileJob>> GetByProjectIdAsync(Guid projectId) =>
            Task.FromResult<IReadOnlyList<CompileJob>>(Jobs.Values.Where(j => j.ProjectId == projectId).ToList());
        public Task AddAsync(CompileJob job) { Jobs[job.Id] = job; return Task.CompletedTask; }
        public Task UpdateAsync(CompileJob job) => Task.CompletedTask;
    }

    private sealed class FakeQueue : ICompileQueue
    {
        public List<Guid> Enqueued { get; } = [];

        public ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default)
        {
            Enqueued.Add(jobId);
            return ValueTask.CompletedTask;
        }

        public ValueTask<Guid> DequeueAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeStorage : IFileStorage
    {
        public StorageProvider Provider => StorageProvider.Local;
        public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Stream?> GetAsync(string key, CancellationToken ct = default) => Task.FromResult<Stream?>(null);
        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(false);
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default) =>
            Task.FromResult($"/files/{key}?expiry={expiry}");
    }
}
