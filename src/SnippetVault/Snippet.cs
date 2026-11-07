namespace SnippetVault;

/// <summary>A single saved code snippet.</summary>
public sealed class Snippet
{
    /// <summary>Short unique id (8 hex chars) used on the command line.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    public string Title { get; set; } = string.Empty;

    /// <summary>Programming language, e.g. "csharp", "python". Stored lowercase.</summary>
    public string Language { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Lowercase, de-duplicated tags.</summary>
    public List<string> Tags { get; set; } = new();

    public bool Favorite { get; set; }

    /// <summary>How many times the snippet was printed with `use`.</summary>
    public int UseCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Normalizes language/tags and stamps UpdatedAt. Call before persisting.</summary>
    public void Normalize()
    {
        Language = Language.Trim().ToLowerInvariant();
        Tags = Tags
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Distinct()
            .OrderBy(t => t)
            .ToList();
        UpdatedAt = DateTime.UtcNow;
    }
}
