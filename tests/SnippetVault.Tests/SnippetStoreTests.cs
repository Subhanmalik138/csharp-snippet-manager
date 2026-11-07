using Xunit;

namespace SnippetVault.Tests;

public sealed class SnippetStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sv-tests-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private string PathFor(string name) => Path.Combine(_dir, name);

    [Fact]
    public void Load_ReturnsEmpty_WhenFileDoesNotExist()
    {
        var store = new SnippetStore(PathFor("missing.json"));
        Assert.Empty(store.Load());
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAllFields()
    {
        var store = new SnippetStore(PathFor("snippets.json"));
        var snippet = new Snippet
        {
            Id = "abc12345",
            Title = "Debounce",
            Language = "TypeScript",
            Code = "const debounce = (fn) => {};",
            Description = "Delays a call.",
            Tags = new List<string> { "Timing", " util " },
            Favorite = true,
            UseCount = 3
        };

        store.Save(new[] { snippet });
        var loaded = store.Load();

        var back = Assert.Single(loaded);
        Assert.Equal("abc12345", back.Id);
        Assert.Equal("Debounce", back.Title);
        Assert.Equal("TypeScript", back.Language);
        Assert.Equal("const debounce = (fn) => {};", back.Code);
        Assert.Equal("Delays a call.", back.Description);
        Assert.Equal(new[] { "Timing", " util " }, back.Tags);
        Assert.True(back.Favorite);
        Assert.Equal(3, back.UseCount);
    }

    [Fact]
    public void Save_CreatesParentDirectory()
    {
        var store = new SnippetStore(Path.Combine(_dir, "nested", "deep", "snippets.json"));
        store.Save(Array.Empty<Snippet>());
        Assert.True(File.Exists(Path.Combine(_dir, "nested", "deep", "snippets.json")));
    }

    [Fact]
    public void Save_LeavesNoTempFilesBehind()
    {
        var store = new SnippetStore(PathFor("snippets.json"));
        store.Save(new[] { new Snippet { Title = "T", Code = "x" } });
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp-*"));
    }
}
