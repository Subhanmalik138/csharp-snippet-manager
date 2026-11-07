using System.Text.Json;

namespace SnippetVault;

/// <summary>
/// Loads and saves the snippet collection as one JSON file.
/// Writes are atomic: the new content goes to a temp file in the same
/// directory and is then moved over the old file, so a crash mid-save
/// can never leave a half-written database behind.
/// </summary>
public sealed class SnippetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    public SnippetStore(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>Default location: ~/.snippetvault/snippets.json (or $SNIPPETVAULT_DATA).</summary>
    public static string DefaultPath()
    {
        var fromEnv = Environment.GetEnvironmentVariable("SNIPPETVAULT_DATA");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".snippetvault", "snippets.json");
    }

    public List<Snippet> Load()
    {
        if (!File.Exists(_filePath))
            return new List<Snippet>();

        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
            return new List<Snippet>();

        return JsonSerializer.Deserialize<List<Snippet>>(json, JsonOptions) ?? new List<Snippet>();
    }

    public void Save(IEnumerable<Snippet> snippets)
    {
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(snippets.ToList(), JsonOptions);
        var tempPath = _filePath + ".tmp-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
