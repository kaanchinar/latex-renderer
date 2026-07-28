using System.Security.Claims;
using LatexEditor.Api.Hubs;
using LatexEditor.Application.Services;
using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace LatexEditor.Api.Tests;

public class ProjectHubTests
{
    private const string OwnerId = "owner-1";
    private const string ConnectionId = "conn-1";

    private readonly ProjectHub _hub;
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly Guid _projectId = Guid.NewGuid();

    public ProjectHubTests()
    {
        var projectRepo = new ProjectRepoStub();
        projectRepo.Projects.Add(new Project { Id = _projectId, Name = "Demo", OwnerId = OwnerId });

        _hub = new ProjectHub(
            new ProjectService(projectRepo),
            new ProjectFileService(projectRepo, new FileRepoStub(), new FakeStorage()),
            new CompileService(projectRepo, new JobRepoStub(), new FakeQueue(), new FakeStorage()))
        {
            Context = CreateContext(OwnerId),
            Groups = _groups
        };
    }

    private static HubCallerContext CreateContext(string userId)
    {
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(ConnectionId);
        context.User.Returns(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)], "test")));
        return context;
    }

    [Fact]
    public async Task JoinProject_Owned_AddsConnectionToProjectGroup()
    {
        await _hub.JoinProject(_projectId);

        await _groups.Received(1).AddToGroupAsync(
            ConnectionId, ProjectHub.GroupName(_projectId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinProject_NotOwned_Throws()
    {
        await Assert.ThrowsAsync<HubException>(() => _hub.JoinProject(Guid.NewGuid()));
    }

    [Fact]
    public async Task TriggerCompile_Owned_ReturnsJobId()
    {
        var jobId = await _hub.TriggerCompile(_projectId);

        Assert.NotEqual(Guid.Empty, jobId);
    }

    [Fact]
    public async Task TriggerCompile_NotOwned_Throws()
    {
        await Assert.ThrowsAsync<HubException>(() => _hub.TriggerCompile(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateFile_Owned_Succeeds()
    {
        await _hub.UpdateFile(_projectId, "main.tex", "content");
    }

    [Fact]
    public async Task UpdateFile_NotOwned_Throws()
    {
        await Assert.ThrowsAsync<HubException>(() => _hub.UpdateFile(Guid.NewGuid(), "main.tex", "x"));
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

    private sealed class FileRepoStub : IProjectFileRepository
    {
        public List<ProjectFile> Files { get; } = [];

        public Task<IReadOnlyList<ProjectFile>> GetByProjectIdAsync(Guid projectId) =>
            Task.FromResult<IReadOnlyList<ProjectFile>>(Files.Where(f => f.ProjectId == projectId).ToList());
        public Task<ProjectFile?> GetByPathAsync(Guid projectId, string path) =>
            Task.FromResult(Files.FirstOrDefault(f => f.ProjectId == projectId && f.Path == path));
        public Task UpsertAsync(ProjectFile file) { Files.Add(file); return Task.CompletedTask; }
        public Task RemoveAsync(Guid projectId, string path) => Task.CompletedTask;
    }

    private sealed class JobRepoStub : ICompileJobRepository
    {
        public List<CompileJob> Jobs { get; } = [];

        public Task<CompileJob?> GetByIdAsync(Guid id) =>
            Task.FromResult(Jobs.FirstOrDefault(j => j.Id == id));
        public Task<IReadOnlyList<CompileJob>> GetByProjectIdAsync(Guid projectId) =>
            Task.FromResult<IReadOnlyList<CompileJob>>(Jobs.Where(j => j.ProjectId == projectId).ToList());
        public Task AddAsync(CompileJob job) { Jobs.Add(job); return Task.CompletedTask; }
        public Task UpdateAsync(CompileJob job) => Task.CompletedTask;
    }

    private sealed class FakeQueue : ICompileQueue
    {
        public ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask<Guid> DequeueAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeStorage : IFileStorage
    {
        public StorageProvider Provider => StorageProvider.Local;
        public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Stream?> GetAsync(string key, CancellationToken ct = default) => Task.FromResult<Stream?>(null);
        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(false);
        public Task DeleteAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default) =>
            Task.FromResult($"/files/{key}");
    }
}
