namespace SnippetVault;

/// <summary>Library-wide numbers printed by the `stats` command.</summary>
public sealed record StatsReport(
    int Total,
    int Favorites,
    int TotalUses,
    IReadOnlyList<(string Language, int Count)> ByLanguage,
    IReadOnlyList<(string Tag, int Count)> TopTags,
    IReadOnlyList<Snippet> MostUsed);
