using System.Text.Json;

namespace SnippetVault;

/// <summary>All snippet operations. Pure logic over <see cref="SnippetStore"/> —
/// no console I/O here, so it is directly unit-testable.</summary>
public sealed class SnippetService
{
    private readonly SnippetStore _store;

    public SnippetService(SnippetStore store)
    {
        _store = store;
    }

    public Snippet Add(Snippet snippet)
    {
        if (string.IsNullOrWhiteSpace(snippet.Title))
            throw new ArgumentException("A title is required.", nameof(snippet));
        if (string.IsNullOrWhiteSpace(snippet.Code))
            throw new ArgumentException("Code is required.", nameof(snippet));

        var all = _store.Load();
        snippet.Normalize();
        if (string.IsNullOrWhiteSpace(snippet.Id))
            snippet.Id = Guid.NewGuid().ToString("N")[..8];
        if (all.Any(s => s.Id.Equals(snippet.Id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A snippet with id '{snippet.Id}' already exists.");

        all.Add(snippet);
        _store.Save(all);
        return snippet;
    }

    /// <summary>Finds a snippet by full id or by an unambiguous id prefix.</summary>
    public Snippet? Get(string idOrPrefix)
    {
        var all = _store.Load();
        var exact = all.FirstOrDefault(s => s.Id.Equals(idOrPrefix, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        var matches = all.Where(s => s.Id.StartsWith(idOrPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    public IReadOnlyList<Snippet> List(string? language = null, string? tag = null,
        bool favoritesOnly = false, string sort = "recent")
    {
        var all = _store.Load().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(language))
            all = all.Where(s => s.Language.Equals(language.Trim().ToLowerInvariant(), StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(tag))
            all = all.Where(s => s.Tags.Contains(tag.Trim().ToLowerInvariant()));
        if (favoritesOnly)
            all = all.Where(s => s.Favorite);

        return sort.ToLowerInvariant() switch
        {
            "title" => all.OrderBy(s => s.Title).ToList(),
            "uses" => all.OrderByDescending(s => s.UseCount).ThenByDescending(s => s.CreatedAt).ToList(),
            _ => all.OrderByDescending(s => s.CreatedAt).ToList(), // "recent"
        };
    }

    public bool Delete(string idOrPrefix)
    {
        var snippet = Get(idOrPrefix);
        if (snippet is null)
            return false;

        var all = _store.Load();
        all.RemoveAll(s => s.Id == snippet.Id);
        _store.Save(all);
        return true;
    }

    /// <summary>Applies <paramref name="mutate"/> to the snippet and saves it.</summary>
    public Snippet? Update(string idOrPrefix, Action<Snippet> mutate)
    {
        var snippet = Get(idOrPrefix);
        if (snippet is null)
            return null;

        mutate(snippet);
        snippet.Normalize();

        var all = _store.Load();
        var index = all.FindIndex(s => s.Id == snippet.Id);
        all[index] = snippet;
        _store.Save(all);
        return snippet;
    }

    /// <summary>
    /// Full-text search. Every query token must appear somewhere; results are
    /// ranked by where tokens matched — title (3) and tags/description (2)
    /// outrank a match inside the code (1).
    /// </summary>
    public IReadOnlyList<Snippet> Search(string query, string? language = null)
    {
        var tokens = Tokenize(query);
        if (tokens.Count == 0)
            return Array.Empty<Snippet>();

        var all = _store.Load().AsEnumerable();
        if (!string.IsNullOrWhiteSpace(language))
            all = all.Where(s => s.Language.Equals(language.Trim().ToLowerInvariant(), StringComparison.Ordinal));

        var scored = new List<(Snippet Snippet, int Score)>();
        foreach (var s in all)
        {
            var score = 0;
            foreach (var token in tokens)
            {
                var inTitle = ContainsToken(s.Title, token);
                var inTags = s.Tags.Any(t => t.Contains(token, StringComparison.OrdinalIgnoreCase));
                var inDesc = ContainsToken(s.Description, token);
                var inCode = s.Code.Contains(token, StringComparison.OrdinalIgnoreCase);

                if (!inTitle && !inTags && !inDesc && !inCode)
                {
                    score = -1; // token missing entirely: snippet does not match
                    break;
                }
                if (inTitle) score += 3;
                if (inTags) score += 2;
                if (inDesc) score += 2;
                if (inCode) score += 1;
            }
            if (score > 0)
                scored.Add((s, score));
        }

        return scored
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Snippet.UseCount)
            .Select(x => x.Snippet)
            .ToList();
    }

    public Snippet? SetFavorite(string idOrPrefix, bool favorite) =>
        Update(idOrPrefix, s => s.Favorite = favorite);

    /// <summary>Bumps the use counter and returns the snippet (for `use`).</summary>
    public Snippet? RecordUse(string idOrPrefix) =>
        Update(idOrPrefix, s => s.UseCount++);

    /// <summary>All tags with how many snippets carry each, most used first.</summary>
    public IReadOnlyList<(string Tag, int Count)> TagCounts()
    {
        return _store.Load()
            .SelectMany(s => s.Tags)
            .GroupBy(t => t)
            .Select(g => (Tag: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Tag)
            .ToList();
    }

    public StatsReport Stats()
    {
        var all = _store.Load();
        return new StatsReport(
            Total: all.Count,
            Favorites: all.Count(s => s.Favorite),
            TotalUses: all.Sum(s => s.UseCount),
            ByLanguage: all.GroupBy(s => s.Language)
                .Select(g => (Language: g.Key, Count: g.Count()))
                .OrderByDescending(x => x.Count).ToList(),
            TopTags: TagCounts().Take(5).ToList(),
            MostUsed: all.OrderByDescending(s => s.UseCount).Take(5).ToList());
    }

    /// <summary>Writes every snippet to a JSON file (same shape as the store).</summary>
    public void Export(string filePath)
    {
        var json = JsonSerializer.Serialize(_store.Load(), new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Reads snippets from a JSON file. With <paramref name="replace"/> the
    /// library is swapped wholesale; otherwise incoming snippets are merged in
    /// and clashing ids get fresh ones. Returns (imported, skipped).
    /// </summary>
    public (int Imported, int Skipped) Import(string filePath, bool replace)
    {
        var incoming = JsonSerializer.Deserialize<List<Snippet>>(
            File.ReadAllText(filePath),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? new List<Snippet>();

        var existing = replace ? new List<Snippet>() : _store.Load();
        var knownIds = existing.Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var imported = 0;
        var skipped = 0;

        foreach (var snippet in incoming)
        {
            if (string.IsNullOrWhiteSpace(snippet.Title) || string.IsNullOrWhiteSpace(snippet.Code))
            {
                skipped++;
                continue;
            }
            snippet.Normalize();
            if (knownIds.Contains(snippet.Id))
                snippet.Id = Guid.NewGuid().ToString("N")[..8];
            knownIds.Add(snippet.Id);
            existing.Add(snippet);
            imported++;
        }

        _store.Save(existing);
        return (imported, skipped);
    }

    private static List<string> Tokenize(string query) =>
        query.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToList();

    private static bool ContainsToken(string text, string token) =>
        text.Contains(token, StringComparison.OrdinalIgnoreCase);
}
