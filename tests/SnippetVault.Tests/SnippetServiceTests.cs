using Xunit;

namespace SnippetVault.Tests;

public sealed class SnippetServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sv-tests-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly SnippetService _service;

    public SnippetServiceTests()
    {
        _service = new SnippetService(new SnippetStore(Path.Combine(_dir, "snippets.json")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private Snippet Make(string title, string lang = "csharp", string code = "code();",
        string desc = "", params string[] tags) =>
        new() { Title = title, Language = lang, Code = code, Description = desc, Tags = tags.ToList() };

    [Fact]
    public void Add_NormalizesLanguageAndTags()
    {
        var added = _service.Add(Make("Retry", "CSharp", tags: new[] { " HTTP ", "retry", "Retry" }));
        Assert.Equal("csharp", added.Language);
        Assert.Equal(new[] { "http", "retry" }, added.Tags);
    }

    [Fact]
    public void Add_RejectsMissingTitleOrCode()
    {
        Assert.Throws<ArgumentException>(() => _service.Add(Make("")));
        Assert.Throws<ArgumentException>(() => _service.Add(Make("T", code: "  ")));
    }

    [Fact]
    public void Add_RejectsDuplicateId()
    {
        var first = _service.Add(Make("One"));
        var clash = Make("Two");
        clash.Id = first.Id;
        Assert.Throws<InvalidOperationException>(() => _service.Add(clash));
    }

    [Fact]
    public void Get_FindsByUniquePrefix()
    {
        var added = _service.Add(Make("Debounce"));
        Assert.Equal(added.Id, _service.Get(added.Id[..4])?.Id);
    }

    [Fact]
    public void Get_ReturnsNull_ForAmbiguousPrefix()
    {
        var store = new SnippetStore(Path.Combine(_dir, "ambig.json"));
        store.Save(new[]
        {
            new Snippet { Id = "abcd1111", Title = "One", Code = "x" },
            new Snippet { Id = "abcd2222", Title = "Two", Code = "y" },
        });
        var service = new SnippetService(store);

        Assert.Null(service.Get("abcd"));
        Assert.Equal("One", service.Get("abcd1")?.Title);
    }

    [Fact]
    public void Get_ReturnsNull_WhenIdUnknown()
    {
        Assert.Null(_service.Get("deadbeef"));
    }

    [Fact]
    public void List_FiltersByLanguageTagAndFavorites()
    {
        var a = _service.Add(Make("A", "python", tags: new[] { "http" }));
        var b = _service.Add(Make("B", "csharp", tags: new[] { "http" }));
        _service.Add(Make("C", "csharp", tags: new[] { "cli" }));
        _service.SetFavorite(b.Id, true);

        Assert.Single(_service.List(language: "python"));
        Assert.Equal(2, _service.List(tag: "http").Count);
        Assert.Single(_service.List(favoritesOnly: true));
        Assert.Equal("B", _service.List(favoritesOnly: true)[0].Title);
    }

    [Fact]
    public void List_SortsByTitleAndUses()
    {
        _service.Add(Make("Zulu"));
        var alpha = _service.Add(Make("Alpha"));
        _service.RecordUse(alpha.Id);
        _service.RecordUse(alpha.Id);

        Assert.Equal("Alpha", _service.List(sort: "title")[0].Title);
        Assert.Equal("Alpha", _service.List(sort: "uses")[0].Title);
    }

    [Fact]
    public void Delete_RemovesAndReportsMissing()
    {
        var added = _service.Add(Make("Gone"));
        Assert.True(_service.Delete(added.Id));
        Assert.Null(_service.Get(added.Id));
        Assert.False(_service.Delete(added.Id));
    }

    [Fact]
    public void Update_EditsFields()
    {
        var added = _service.Add(Make("Old", tags: new[] { "x" }));
        var updated = _service.Update(added.Id, s =>
        {
            s.Title = "New";
            s.Tags = new List<string> { "y" };
        });

        Assert.Equal("New", updated!.Title);
        Assert.Equal(new[] { "y" }, _service.Get(added.Id)!.Tags);
    }

    [Fact]
    public void Search_RanksTitleMatchesAboveCodeMatches()
    {
        _service.Add(Make("Other", code: "retry the request"));
        var titled = _service.Add(Make("Retry helper", code: "unrelated body"));

        var results = _service.Search("retry");
        Assert.Equal(2, results.Count);
        Assert.Equal(titled.Id, results[0].Id);
    }

    [Fact]
    public void Search_RequiresEveryToken()
    {
        _service.Add(Make("Retry helper", code: "backoff policy"));
        Assert.Empty(_service.Search("retry missingword"));
        Assert.Single(_service.Search("retry backoff"));
    }

    [Fact]
    public void Search_MatchesTagsAndDescription()
    {
        _service.Add(Make("Plain", desc: "handles http retries", tags: new[] { "polly" }));
        Assert.Single(_service.Search("polly"));
        Assert.Single(_service.Search("http retries"));
    }

    [Fact]
    public void Search_IsCaseInsensitive()
    {
        _service.Add(Make("Retry Helper"));
        Assert.Single(_service.Search("RETRY"));
    }

    [Fact]
    public void SetFavorite_TogglesFlag()
    {
        var added = _service.Add(Make("Fav"));
        _service.SetFavorite(added.Id, true);
        Assert.True(_service.Get(added.Id)!.Favorite);
        _service.SetFavorite(added.Id, false);
        Assert.False(_service.Get(added.Id)!.Favorite);
    }

    [Fact]
    public void RecordUse_IncrementsCounter()
    {
        var added = _service.Add(Make("Used"));
        _service.RecordUse(added.Id);
        _service.RecordUse(added.Id);
        Assert.Equal(2, _service.Get(added.Id)!.UseCount);
    }

    [Fact]
    public void TagCounts_AggregatesAcrossSnippets()
    {
        _service.Add(Make("A", tags: new[] { "http", "cli" }));
        _service.Add(Make("B", tags: new[] { "http" }));

        var counts = _service.TagCounts().ToDictionary(x => x.Tag, x => x.Count);
        Assert.Equal(2, counts["http"]);
        Assert.Equal(1, counts["cli"]);
    }

    [Fact]
    public void Stats_SummarizesLibrary()
    {
        var a = _service.Add(Make("A", "python"));
        _service.Add(Make("B", "csharp"));
        _service.SetFavorite(a.Id, true);
        _service.RecordUse(a.Id);

        var stats = _service.Stats();
        Assert.Equal(2, stats.Total);
        Assert.Equal(1, stats.Favorites);
        Assert.Equal(1, stats.TotalUses);
        Assert.Equal(2, stats.ByLanguage.Count);
        Assert.Equal("A", stats.MostUsed[0].Title);
    }

    [Fact]
    public void ExportImport_RoundTrips()
    {
        _service.Add(Make("A", tags: new[] { "http" }));
        var exportPath = Path.Combine(_dir, "export.json");
        _service.Export(exportPath);

        var other = new SnippetService(new SnippetStore(Path.Combine(_dir, "other.json")));
        var (imported, skipped) = other.Import(exportPath, replace: false);

        Assert.Equal((1, 0), (imported, skipped));
        Assert.Single(other.List());
    }

    [Fact]
    public void Import_Merge_ReassignsClashingIds()
    {
        var added = _service.Add(Make("A"));
        var exportPath = Path.Combine(_dir, "export.json");
        _service.Export(exportPath);

        var (imported, _) = _service.Import(exportPath, replace: false);
        Assert.Equal(1, imported);
        Assert.Equal(2, _service.List().Count);
        Assert.Equal(2, _service.List().Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void Import_Replace_SwapsLibrary()
    {
        _service.Add(Make("Old"));
        var exportPath = Path.Combine(_dir, "export.json");
        var other = new SnippetService(new SnippetStore(Path.Combine(_dir, "other.json")));
        other.Add(Make("New"));
        other.Export(exportPath);

        _service.Import(exportPath, replace: true);
        Assert.Single(_service.List());
        Assert.Equal("New", _service.List()[0].Title);
    }

    [Fact]
    public void Import_SkipsSnippetsWithoutTitleOrCode()
    {
        Directory.CreateDirectory(_dir);
        var exportPath = Path.Combine(_dir, "bad.json");
        File.WriteAllText(exportPath,
            """[{"id":"11111111","title":"","code":"x","language":"csharp","tags":[],"favorite":false,"useCount":0,"createdAt":"2026-01-01T00:00:00Z","updatedAt":"2026-01-01T00:00:00Z"},{"id":"22222222","title":"Good","code":"y","language":"csharp","tags":[],"favorite":false,"useCount":0,"createdAt":"2026-01-01T00:00:00Z","updatedAt":"2026-01-01T00:00:00Z"}]""");

        var (imported, skipped) = _service.Import(exportPath, replace: false);
        Assert.Equal((1, 1), (imported, skipped));
    }
}
