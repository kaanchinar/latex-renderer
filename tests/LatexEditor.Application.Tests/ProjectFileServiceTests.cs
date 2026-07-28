using System.Text;
using LatexEditor.Application.DTOs;
using LatexEditor.Application.Services;
using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;

namespace LatexEditor.Application.Tests;

public class ProjectFileServiceTests
{
    private const string OwnerId = "owner-1";
    private const string OtherUserId = "owner-2";

    private readonly InMemoryProjectRepositoryStub _projectRepo = new();
    private readonly InMemoryProjectFileRepositoryStub _fileRepo = new();
    private readonly FakeFileStorage _storage = new();
    private readonly ProjectFileService _service;

    public ProjectFileServiceTests()
    {
        _service = new ProjectFileService(_projectRepo, _fileRepo, _storage);
    }

    private Guid SeedProject(string ownerId = OwnerId)
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "Demo", OwnerId = ownerId };
        _projectRepo.Projects.Add(project);
        return project.Id;
    }

    [Fact]
    public async Task Upsert_NewFile_WritesContentToStorageAndMetadata()
    {
        var projectId = SeedProject();

        var result = await _service.UpsertAsync(projectId, "main.tex", new UpsertFileDto { Content = "\\documentclass{article}" }, OwnerId);

        Assert.NotNull(result);
        Assert.Equal("main.tex", result.Path);
        Assert.Equal("\\documentclass{article}", result.Content);

        var metadata = await _fileRepo.GetByPathAsync(projectId, "main.tex");
        Assert.NotNull(metadata);
        Assert.Equal(StorageProvider.Local, metadata.StorageProvider);
        Assert.False(string.IsNullOrEmpty(metadata.StorageKey));
        Assert.Equal("\\documentclass{article}", _storage.ReadText(metadata.StorageKey));
    }

    [Fact]
    public async Task Upsert_ExistingFile_PreservesIdAndStorageKey()
    {
        var projectId = SeedProject();
        var first = await _service.UpsertAsync(projectId, "main.tex", new UpsertFileDto { Content = "v1" }, OwnerId);

        var second = await _service.UpsertAsync(projectId, "main.tex", new UpsertFileDto { Content = "v2" }, OwnerId);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Id, second.Id);

        var metadata = await _fileRepo.GetByPathAsync(projectId, "main.tex");
        Assert.NotNull(metadata);
        Assert.Equal("v2", _storage.ReadText(metadata.StorageKey));
        Assert.Single(_fileRepo.Files);
    }

    [Fact]
    public async Task GetByPath_ExistingFile_ReturnsContentFromStorage()
    {
        var projectId = SeedProject();
        await _service.UpsertAsync(projectId, "main.tex", new UpsertFileDto { Content = "hello" }, OwnerId);

        var result = await _service.GetByPathAsync(projectId, "main.tex", OwnerId);

        Assert.NotNull(result);
        Assert.Equal("hello", result.Content);
    }

    [Fact]
    public async Task GetByProjectId_ReturnsMetadataWithoutContent()
    {
        var projectId = SeedProject();
        await _service.UpsertAsync(projectId, "main.tex", new UpsertFileDto { Content = "hello" }, OwnerId);

        var files = await _service.GetByProjectIdAsync(projectId, OwnerId);

        var file = Assert.Single(files);
        Assert.Equal("main.tex", file.Path);
        Assert.Equal(string.Empty, file.Content);
    }

    [Fact]
    public async Task Delete_RemovesContentAndMetadata()
    {
        var projectId = SeedProject();
        await _service.UpsertAsync(projectId, "main.tex", new UpsertFileDto { Content = "hello" }, OwnerId);
        var storageKey = (await _fileRepo.GetByPathAsync(projectId, "main.tex"))!.StorageKey;

        var deleted = await _service.DeleteAsync(projectId, "main.tex", OwnerId);

        Assert.True(deleted);
        Assert.Null(await _fileRepo.GetByPathAsync(projectId, "main.tex"));
        Assert.False(_storage.Objects.ContainsKey(storageKey));
    }

    [Theory]
    [InlineData("main.tex", OtherUserId)]
    [InlineData("missing.tex", OwnerId)]
    public async Task GetByPath_WrongOwnerOrMissingFile_ReturnsNull(string path, string userId)
    {
        var projectId = SeedProject();
        await _service.UpsertAsync(projectId, "main.tex", new UpsertFileDto { Content = "hello" }, OwnerId);

        Assert.Null(await _service.GetByPathAsync(projectId, path, userId));
    }

    [Fact]
    public async Task Upsert_WrongOwner_ReturnsNullAndWritesNothing()
    {
        var projectId = SeedProject();

        var result = await _service.UpsertAsync(projectId, "main.tex", new UpsertFileDto { Content = "x" }, OtherUserId);

        Assert.Null(result);
        Assert.Empty(_fileRepo.Files);
        Assert.Empty(_storage.Objects);
    }

    [Fact]
    public async Task Delete_WrongOwner_ReturnsFalseAndKeepsFile()
    {
        var projectId = SeedProject();
        await _service.UpsertAsync(projectId, "main.tex", new UpsertFileDto { Content = "x" }, OwnerId);

        var deleted = await _service.DeleteAsync(projectId, "main.tex", OtherUserId);

        Assert.False(deleted);
        Assert.Single(_fileRepo.Files);
    }

    private sealed class InMemoryProjectRepositoryStub : IProjectRepository
    {
        public List<Project> Projects { get; } = [];

        public Task<IReadOnlyList<Project>> GetByOwnerAsync(string ownerId) =>
            Task.FromResult<IReadOnlyList<Project>>(Projects.Where(p => p.OwnerId == ownerId).ToList());

        public Task<Project?> GetByIdAsync(Guid id, string ownerId) =>
            Task.FromResult(Projects.FirstOrDefault(p => p.Id == id && p.OwnerId == ownerId));

        public Task AddAsync(Project project) { Projects.Add(project); return Task.CompletedTask; }
        public Task UpdateAsync(Project project) => Task.CompletedTask;
        public Task RemoveAsync(Project project) { Projects.Remove(project); return Task.CompletedTask; }
    }

    private sealed class InMemoryProjectFileRepositoryStub : IProjectFileRepository
    {
        public List<ProjectFile> Files { get; } = [];

        public Task<IReadOnlyList<ProjectFile>> GetByProjectIdAsync(Guid projectId) =>
            Task.FromResult<IReadOnlyList<ProjectFile>>(Files.Where(f => f.ProjectId == projectId).ToList());

        public Task<ProjectFile?> GetByPathAsync(Guid projectId, string path) =>
            Task.FromResult(Files.FirstOrDefault(f => f.ProjectId == projectId && f.Path == path));

        public Task UpsertAsync(ProjectFile file)
        {
            var existing = Files.FirstOrDefault(f => f.ProjectId == file.ProjectId && f.Path == file.Path);
            if (existing is null) Files.Add(file);
            else
            {
                existing.StorageKey = file.StorageKey;
                existing.StorageProvider = file.StorageProvider;
                existing.IsBinary = file.IsBinary;
                existing.UpdatedAt = file.UpdatedAt;
            }
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid projectId, string path)
        {
            Files.RemoveAll(f => f.ProjectId == projectId && f.Path == path);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFileStorage : IFileStorage
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
            Task.FromResult<Stream?>(Objects.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);

        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(Objects.ContainsKey(key));

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            Objects.Remove(key);
            return Task.CompletedTask;
        }

        public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default) =>
            Task.FromResult($"/files/{key}");

        public string ReadText(string key) => Encoding.UTF8.GetString(Objects[key]);
    }
}
