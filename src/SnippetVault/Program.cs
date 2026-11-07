namespace SnippetVault;

internal static class Program
{
    private const string Version = "1.0.0";

    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
            return 0;
        }

        if (args[0] is "--version" or "-v")
        {
            Console.WriteLine($"snippetvault {Version}");
            return 0;
        }

        // Global option: --data <path> selects the snippet database file.
        var dataPath = SnippetStore.DefaultPath();
        var rest = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--data" && i + 1 < args.Length)
            {
                dataPath = args[++i];
            }
            else if (args[i].StartsWith("--data=", StringComparison.Ordinal))
            {
                dataPath = args[i]["--data=".Length..];
            }
            else
            {
                rest.Add(args[i]);
            }
        }

        var service = new SnippetService(new SnippetStore(dataPath));
        var command = rest[0];
        var cmdArgs = Args.Parse(rest.Skip(1).ToArray());

        try
        {
            return command switch
            {
                "add" => Add(service, cmdArgs),
                "list" => List(service, cmdArgs),
                "show" => Show(service, cmdArgs),
                "delete" => Delete(service, cmdArgs),
                "edit" => Edit(service, cmdArgs),
                _ => Unknown(command),
            };
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine($"""
            snippetvault {Version} — keep the code you keep re-typing, one command away.

            Usage: snippetvault [--data <path>] <command> [options]

            Commands:
              add     --title T --lang L (--code C | --file F) [--tags a,b] [--desc D]
              list    [--lang L] [--tag T] [--favorites] [--sort recent|title|uses]
              show    <id>                     print one snippet with its code
              edit    <id> [--title T] [--lang L] [--code C | --file F] [--tags a,b] [--desc D]
              delete  <id>

            Global options:
              --data <path>   snippet database file (default ~/.snippetvault/snippets.json,
                              or $SNIPPETVAULT_DATA)

            IDs are 8 characters; a unique prefix is enough, e.g. `show a1b2`.
            """);
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"error: unknown command '{command}'. Run `snippetvault --help`.");
        return 2;
    }

    private static int Add(SnippetService service, Args args)
    {
        var title = args.Option("title") ?? throw new ArgumentException("--title is required.");
        var lang = args.Option("lang") ?? throw new ArgumentException("--lang is required.");

        var code = args.Option("code");
        if (code is null && args.Option("file") is { } file)
            code = File.ReadAllText(file);
        if (code is null)
            throw new ArgumentException("Provide the code with --code or --file.");

        var snippet = service.Add(new Snippet
        {
            Title = title,
            Language = lang,
            Code = code,
            Description = args.Option("desc") ?? string.Empty,
            Tags = SplitTags(args.Option("tags")),
        });
        Console.WriteLine($"saved [{snippet.Id}] {snippet.Title} ({snippet.Language})");
        return 0;
    }

    private static int List(SnippetService service, Args args)
    {
        var snippets = service.List(
            args.Option("lang"),
            args.Option("tag"),
            args.Has("favorites"),
            args.Option("sort") ?? "recent");

        if (snippets.Count == 0)
        {
            Console.WriteLine("No snippets yet. Add one with `snippetvault add --help`.");
            return 0;
        }

        foreach (var s in snippets)
            Console.WriteLine(FormatRow(s));
        Console.WriteLine($"{snippets.Count} snippet(s)");
        return 0;
    }

    private static int Show(SnippetService service, Args args)
    {
        if (args.Positionals.Count == 0)
            throw new ArgumentException("show needs an id: `snippetvault show <id>`.");
        var snippet = service.Get(args.Positionals[0]);
        if (snippet is null)
        {
            Console.Error.WriteLine($"error: no snippet with id '{args.Positionals[0]}'.");
            return 1;
        }

        Console.WriteLine($"{(snippet.Favorite ? "★ " : "")}{snippet.Title} [{snippet.Id}]");
        Console.WriteLine($"Language: {snippet.Language}   Uses: {snippet.UseCount}   Created: {snippet.CreatedAt:yyyy-MM-dd}");
        if (snippet.Tags.Count > 0)
            Console.WriteLine($"Tags: {string.Join(", ", snippet.Tags)}");
        if (!string.IsNullOrWhiteSpace(snippet.Description))
            Console.WriteLine(snippet.Description);
        Console.WriteLine(new string('-', 40));
        Console.WriteLine(snippet.Code);
        return 0;
    }

    private static int Delete(SnippetService service, Args args)
    {
        if (args.Positionals.Count == 0)
            throw new ArgumentException("delete needs an id: `snippetvault delete <id>`.");
        if (!service.Delete(args.Positionals[0]))
        {
            Console.Error.WriteLine($"error: no snippet with id '{args.Positionals[0]}'.");
            return 1;
        }
        Console.WriteLine($"deleted {args.Positionals[0]}");
        return 0;
    }

    private static int Edit(SnippetService service, Args args)
    {
        if (args.Positionals.Count == 0)
            throw new ArgumentException("edit needs an id: `snippetvault edit <id> [options]`.");

        var updated = service.Update(args.Positionals[0], s =>
        {
            if (args.Option("title") is { } title) s.Title = title;
            if (args.Option("lang") is { } lang) s.Language = lang;
            if (args.Option("code") is { } code) s.Code = code;
            else if (args.Option("file") is { } file) s.Code = File.ReadAllText(file);
            if (args.Option("desc") is { } desc) s.Description = desc;
            if (args.Option("tags") is { } tags) s.Tags = SplitTags(tags);
        });

        if (updated is null)
        {
            Console.Error.WriteLine($"error: no snippet with id '{args.Positionals[0]}'.");
            return 1;
        }
        Console.WriteLine($"updated [{updated.Id}] {updated.Title}");
        return 0;
    }

    private static List<string> SplitTags(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? new List<string>()
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string FormatRow(Snippet s)
    {
        var star = s.Favorite ? "★ " : "  ";
        var tags = s.Tags.Count > 0 ? $"  #{string.Join(" #", s.Tags)}" : "";
        return $"{star}{s.Id}  {s.Title}  ({s.Language}){tags}";
    }
}
