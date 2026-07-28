using System.Text;
using LatexEditor.Core.Entities;
using LatexEditor.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace LatexEditor.Infrastructure.Tests;

public class LocalFileStorageTests : IDisposable
{
    private readonly string _root;
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"latex-storage-tests-{Guid.NewGuid():N}");
        _storage = new LocalFileStorage(Options.Create(new StorageOptions { LocalRootPath = _root }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static MemoryStream ToStream(string content) => new(Encoding.UTF8.GetBytes(content));

    private static async Task<string> ReadStringAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task Put_ThenGet_ReturnsSameContent()
    {
        await _storage.PutAsync("proj/main.tex", ToStream("hello"), "text/plain");

        await using var stream = await _storage.GetAsync("proj/main.tex");

        Assert.NotNull(stream);
        Assert.Equal("hello", await ReadStringAsync(stream));
    }

    [Fact]
    public async Task Put_SameKeyTwice_OverwritesContent()
    {
        await _storage.PutAsync("a.tex", ToStream("first"), "text/plain");
        await _storage.PutAsync("a.tex", ToStream("second"), "text/plain");

        await using var stream = await _storage.GetAsync("a.tex");

        Assert.NotNull(stream);
        Assert.Equal("second", await ReadStringAsync(stream));
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsNull()
    {
        Assert.Null(await _storage.GetAsync("missing.tex"));
    }

    [Fact]
    public async Task Exists_ReflectsPutAndDelete()
    {
        Assert.False(await _storage.ExistsAsync("a.tex"));

        await _storage.PutAsync("a.tex", ToStream("x"), "text/plain");
        Assert.True(await _storage.ExistsAsync("a.tex"));

        await _storage.DeleteAsync("a.tex");
        Assert.False(await _storage.ExistsAsync("a.tex"));
    }

    [Fact]
    public async Task Delete_MissingKey_DoesNotThrow()
    {
        await _storage.DeleteAsync("missing.tex");
    }

    [Theory]
    [InlineData("../escape.tex")]
    [InlineData("a/../../escape.tex")]
    public async Task Put_KeyEscapingRoot_Throws(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.PutAsync(key, ToStream("x"), "text/plain"));
    }

    [Fact]
    public void Provider_IsLocal()
    {
        Assert.Equal(StorageProvider.Local, _storage.Provider);
    }
}
