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
}
