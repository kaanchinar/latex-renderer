using LatexEditor.Application.DTOs;
using LatexEditor.Application.Services;
using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;

namespace LatexEditor.Application.Tests;

public class ProjectServiceTests
{
    private const string OwnerId = "owner-1";

    private readonly ProjectRepositoryStub _repo = new();
    private readonly ProjectService _service;

    public ProjectServiceTests()
    {
        _service = new ProjectService(_repo);
    }

    [Fact]
    public async Task Create_ValidName_PersistsProjectWithOwner()
    {
        var result = await _service.CreateAsync(new CreateProjectDto { Name = "Thesis" }, OwnerId);

        Assert.Equal("Thesis", result.Name);
        var stored = Assert.Single(_repo.Projects);
        Assert.Equal(OwnerId, stored.OwnerId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_EmptyName_Throws(string name)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateProjectDto { Name = name }, OwnerId));
    }

    [Fact]
    public async Task GetById_WrongOwner_ReturnsNull()
    {
        var created = await _service.CreateAsync(new CreateProjectDto { Name = "Thesis" }, OwnerId);

        Assert.Null(await _service.GetByIdAsync(created.Id, "someone-else"));
    }

    [Fact]
    public async Task Update_RenamesProject()
    {
        var created = await _service.CreateAsync(new CreateProjectDto { Name = "Old" }, OwnerId);

        var updated = await _service.UpdateAsync(created.Id, new UpdateProjectDto { Name = "New" }, OwnerId);

        Assert.NotNull(updated);
        Assert.Equal("New", updated.Name);
    }

    [Fact]
    public async Task Delete_RemovesProject()
    {
        var created = await _service.CreateAsync(new CreateProjectDto { Name = "Thesis" }, OwnerId);

        Assert.True(await _service.DeleteAsync(created.Id, OwnerId));
        Assert.Empty(_repo.Projects);
    }

    [Fact]
    public async Task Create_GeneratesUrlFriendlySlug()
    {
        var result = await _service.CreateAsync(new CreateProjectDto { Name = "My Thesis Draft!" }, OwnerId);

        Assert.Equal("my-thesis-draft", result.Slug);
    }

    [Fact]
    public async Task Create_SameNameForSameOwner_AppendsUniqueSuffix()
    {
        var first = await _service.CreateAsync(new CreateProjectDto { Name = "My Project" }, OwnerId);
        var second = await _service.CreateAsync(new CreateProjectDto { Name = "My Project" }, OwnerId);
        var third = await _service.CreateAsync(new CreateProjectDto { Name = "My Project" }, OwnerId);

        Assert.Equal("my-project", first.Slug);
        Assert.Equal("my-project-2", second.Slug);
        Assert.Equal("my-project-3", third.Slug);
    }

    [Fact]
    public async Task Create_SameNameForDifferentOwners_ReusesBaseSlug()
    {
        var first = await _service.CreateAsync(new CreateProjectDto { Name = "My Project" }, OwnerId);
        var second = await _service.CreateAsync(new CreateProjectDto { Name = "My Project" }, "owner-2");

        Assert.Equal("my-project", first.Slug);
        Assert.Equal("my-project", second.Slug);
    }

    [Theory]
    [InlineData("Simple", "simple")]
    [InlineData("  Spaces  Around  ", "spaces-around")]
    [InlineData("multiple---dashes", "multiple-dashes")]
    [InlineData("sp3c!al ch@rs", "sp3c-al-ch-rs")]
    [InlineData("---leading-and-trailing---", "leading-and-trailing")]
    public void GenerateSlug_ProducesUrlFriendlySlugs(string name, string expected)
    {
        Assert.Equal(expected, ProjectService.GenerateSlug(name));
    }

    private sealed class ProjectRepositoryStub : IProjectRepository
    {
        public List<Project> Projects { get; } = [];

        public Task<IReadOnlyList<Project>> GetByOwnerAsync(string ownerId) =>
            Task.FromResult<IReadOnlyList<Project>>(Projects.Where(p => p.OwnerId == ownerId).ToList());

        public Task<Project?> GetByIdAsync(Guid id, string ownerId) =>
            Task.FromResult(Projects.FirstOrDefault(p => p.Id == id && p.OwnerId == ownerId));

        public Task<Project?> GetByIdUnrestrictedAsync(Guid id) =>
            Task.FromResult(Projects.FirstOrDefault(p => p.Id == id));

        public Task AddAsync(Project project) { Projects.Add(project); return Task.CompletedTask; }

        public Task UpdateAsync(Project project)
        {
            var existing = Projects.First(p => p.Id == project.Id);
            existing.Name = project.Name;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Project project) { Projects.Remove(project); return Task.CompletedTask; }
    }
}
