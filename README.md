# SnippetVault

A command-line code snippet manager for the bits of code you keep re-typing:
one command to save them, one command to find them again.

```console
$ snippetvault add --title "Retry with backoff" --lang csharp \
      --code "await Policy.Handle<Exception>().WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(i));" \
      --tags retry,http --desc "Polly-style retry for flaky HTTP calls"
saved [00e18495] Retry with backoff (csharp)

$ snippetvault search retry
  00e18495  Retry with backoff  (csharp)  #http #retry
1 match(es)

$ snippetvault use 00e18495 | pbcopy   # raw code on stdout — pipe it anywhere
```

## Why this exists

Every developer accumulates a private pile of copy-paste fragments: the
debounce function, the date formatter, the SQL upsert, the regex that took
an hour to get right. They end up scattered across gists, notes apps, and
old projects — or lost entirely. SnippetVault keeps them in one searchable
place on your own machine, with no account and no sync service involved.

## Features

- **Save snippets** from the command line (`--code`) or from a file
  (`--file`), with a title, language, description, and tags.
- **Full-text search** across title, tags, description, and code. Every
  query word must match; results are ranked so a title hit beats a hit
  buried inside the code.
- **Filter and sort** listings by language, tag, or favorites, sorted by
  recency, title, or use count.
- **Favorites** (`fav` / `unfav`) for the snippets you reach for daily.
- **Usage tracking** — `use <id>` prints just the raw code (so it pipes
  straight into a clipboard or file) and bumps a counter; `stats` shows
  your most-used snippets.
- **Short ids** — every snippet gets an 8-character id, and a unique
  prefix is enough: `show a1b2` works.
- **Import / export** — dump the whole library to JSON, merge a dump from
  another machine (clashing ids get fresh ones), or replace the library
  wholesale with `--replace`.
- **Safe storage** — snippets live in a single JSON file
  (`~/.snippetvault/snippets.json`, or `$SNIPPETVAULT_DATA`, or `--data`),
  written atomically (temp file + rename) so a crash can never corrupt it.

## Install

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```console
git clone https://github.com/Subhanmalik138/csharp-snippet-manager.git
cd csharp-snippet-manager
dotnet build src/SnippetVault -c Release
# the binary is at src/SnippetVault/bin/Release/net8.0/snippetvault
# (or `dotnet publish src/SnippetVault -c Release -o ~/bin` to install it)
```

No third-party packages: the app uses only the .NET base class library,
and the test project uses xUnit.

## Usage

```
snippetvault [--data <path>] <command> [options]

add     --title T --lang L (--code C | --file F) [--tags a,b] [--desc D]
list    [--lang L] [--tag T] [--favorites] [--sort recent|title|uses]
show    <id>                     print one snippet with its code
search  <query> [--lang L]       full-text search, best match first
edit    <id> [--title T] [--lang L] [--code C | --file F] [--tags a,b] [--desc D]
delete  <id>
fav     <id>                     mark as favorite
unfav   <id>                     remove favorite
use     <id>                     print just the code, count the use
tags                             list every tag with its snippet count
stats                            library totals, top languages and most-used
export  --file F                 dump the whole library to JSON
import  --file F [--replace]     merge a JSON dump in (or replace the library)
```

A few everyday flows:

```console
# save a whole file as a snippet
snippetvault add --title "Debounce" --lang typescript --file debounce.ts --tags timing,util

# everything in Python about HTTP, most used first
snippetvault list --lang python --tag http --sort uses

# what do I actually reach for?
snippetvault stats

# back up, or move to another machine
snippetvault export --file snippets-backup.json
snippetvault import --file snippets-backup.json
```

## Design decisions

- **One JSON file, no database.** A personal snippet library is small
  (hundreds of entries, not millions). A single file is trivially
  inspectable, greppable, and back-up-able, and it keeps the tool
  dependency-free. The atomic write (temp file + move) is the one
  concession to durability.
- **Logic separated from the CLI.** `SnippetService` holds every operation
  and knows nothing about the console; `Program` only parses arguments
  and prints. That split is what makes the service directly unit-testable
  without spawning processes.
- **Search is ranked, not boolean.** A snippet that matches in its title
  is almost always the one you want; a match inside the code body is a
  weaker signal. Weighting title (3) > tags/description (2) > code (1)
  and requiring every token keeps results short and relevant without
  needing a real search index.
- **IDs are short, opaque, and prefix-addressable.** UUIDs are unreadable
  on a command line; sequential numbers leak ordering and collide on
  import. Eight hex characters plus unique-prefix lookup is the middle
  ground, and import reassigns ids that clash.

## Project layout

```
src/SnippetVault/
  Program.cs        CLI parsing and command handlers
  SnippetService.cs all snippet operations (CRUD, search, stats, import/export)
  SnippetStore.cs   atomic JSON persistence
  Snippet.cs        the snippet model
  StatsReport.cs    stats result type
  Args.cs           minimal --flag argument parser
tests/SnippetVault.Tests/
  SnippetServiceTests.cs   22 tests
  SnippetStoreTests.cs     4 tests
```

## Tests

```console
dotnet test
```

26 xUnit tests cover the store round-trip, CRUD validation, id-prefix
lookup, list filters and sorts, search ranking and token rules,
favorites and use counting, tag aggregation, stats, and the import/export
merge/replace/skip rules.

## License

MIT — see [LICENSE](LICENSE).
