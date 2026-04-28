# Music Sorter

A Windows WPF app that takes a folder of badly-named music (typical YouTube
rips: `Artist - Song (Official Music Video) [HD].mp3`) and rewrites it into a
clean library:

* strips YouTube cruft from filenames (`(Official Music Video)`,
  `[Lyrics]`, `(HD)`, trailing 11-char video IDs, uploader tags...)
* identifies each track by cascading through every available method until a
  confident match is found:
  - existing ID3 tags (always read first to avoid clobbering good metadata)
  1. cleaned filename
  2. MusicBrainz title/artist search (free, no key)
  3. AcoustID audio fingerprint (needs Chromaprint `fpcalc.exe` + a free key)
  4. Shazam text search (needs a RapidAPI key)
* writes the merged result to the destination MP3 — with full control over
  *what* gets written: full ID3 enrichment, fill-blanks-only, minimal
  (Title + Artist), or **strip every tag** for an audio-only file.
* embeds cover art from Cover Art Archive or Shazam — or **strips embedded
  art entirely** if you'd rather have plain audio.
* detects duplicates against the existing library (Artist+Title, recording
  MBID, Artist+Duration ±2s, SHA-1) and **keeps the highest-quality copy**,
  sending the loser to the Recycle Bin.
* moves (or copies) the file into a destination layout you pick at run-time.
* anything that can't be confidently identified lands in `_Unsorted/` so a
  manual sweep is easy.

## Building the .exe

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download).
Then on Windows:

```
build.cmd
```

The single-file, self-contained executable is produced at
`publish\MusicSorter.exe`. You can copy that one file anywhere — it does not
need .NET installed on the target machine.

To build from PowerShell or any shell:

```
dotnet publish MusicSorter\MusicSorter.csproj -c Release -r win-x64 ^
    --self-contained true -p:PublishSingleFile=true -o publish
```

## Optional: AcoustID fingerprinting

Audio fingerprint lookup needs `fpcalc.exe` from the
[Chromaprint](https://acoustid.org/chromaprint) project.

1. Download the Windows zip from <https://acoustid.org/chromaprint>.
2. Drop `fpcalc.exe` next to `MusicSorter.exe` (or anywhere on `PATH`, or
   point to it from the *fpcalc.exe path* field in the app).
3. Get a free AcoustID application key at <https://acoustid.org/new-application>
   and paste it into the app.

Without `fpcalc.exe` or a key, the AcoustID step is silently skipped and the
pipeline falls back to MusicBrainz / Shazam.

## Optional: Shazam

The Shazam step uses [RapidAPI](https://rapidapi.com/apidojo/api/shazam) — sign
up, subscribe to the free tier, copy your `x-rapidapi-key`, and paste it into
the app. The default host is `shazam.p.rapidapi.com`; alternate hosts are in
the dropdown.

## Run-time controls

* **Source / Output** — folder pickers (the source is scanned recursively).
* **Folder layout** — pick from:
  * `Artist / Album / <file>`
  * `AlbumArtist / Album / <file>`
  * `Artist / <file>`
  * `Letter / Artist / <file>` — A / Abba, B / Blink-182. "The Beatles"
    goes under B (leading "The " is stripped); digits go to "0-9";
    anything else (e.g. `!!!`, `*NSYNC`) goes to `#`.
  * `Letter / Artist / Album / <file>` — same letter bucketing but with
    the album subfolder kept.
  * `Letter / <file>` — letter bucket only, no artist subfolder. Pair
    with the per-folder cap (e.g. 99) so a busy letter splits into
    `A`, `A (2)`, `A (3)`, ... and pair with a file-name pattern that
    keeps the artist (e.g. *Artist - Title.mp3*) since the path no
    longer carries it.
  * `Genre / Artist / Album / <file>`
  * `Year / Artist / Album / <file>`
  * `Flat (no subfolders)`
* **File name** — pick from `01 - Title.mp3`, `Artist - Title.mp3`,
  `01 - Artist - Title.mp3`, or `Title.mp3`.
* **Per-folder cap** — optionally limit the number of `.mp3` files in
  any one leaf folder (default 99 when enabled). When the natural target
  folder is full, files are redirected to `Folder (2)`, `Folder (3)`, etc.
  Useful for layouts like *Letter / Artist / <file>* where one letter
  can otherwise accumulate hundreds of files.
* **Identification methods** — toggle each step independently.
* **ID3 tag mode** (every possible representation):
  * `Full enrichment` — overwrite tags with the matched metadata.
  * `Fill blanks only` — only write fields that are currently empty.
  * `Minimal` — write only Title + Artist, clear everything else.
  * `Strip all tags` — leave the file with **no** ID3 tags at all
    (audio-only, organized purely by filename / folder layout).
* **Cover-art mode**:
  * `Download + embed` — fetch from Cover Art Archive / Shazam and embed.
  * `Keep existing` — never touch existing embedded art.
  * `Strip all art` — remove every embedded picture (audio-only).
* **File mode** — *Move* (cuts originals from the source) or *Copy*.
* **Skip if destination file already exists** — re-run the sort safely.

### Duplicate handling

Music Sorter detects duplicates against the existing library *and* against
files already processed earlier in the same run, then keeps the
highest-quality copy.

* **Detection keys** (any combination):
  * `Artist + Title` — case-insensitive, punctuation-stripped.
  * `Recording / MBID` — same MusicBrainz / AcoustID recording-id.
  * `Artist + Duration ±2s` — catches different encodings of the same song.
  * `File hash (SHA-1)` — exact byte-identical files.
  * `…also hash existing library` — opt-in. Off by default because hashing
    every file in a 10K-track library is slow; with it off, hashes only
    catch in-batch duplicates.
* **Action**:
  * `Keep highest-quality, remove others` — bitrate, then file size, then
    duration. The lower-quality copy is sent to the **Recycle Bin** by
    default (uncheck *Send removed duplicates to Recycle Bin* for a
    permanent delete).
  * `Log only (rename to '(2).mp3')` — disable automatic removal.

### Pure "just rename, do nothing else" mode

If you only want to clean up filenames in place — no library reorganisation,
no API calls, no tag changes — pick:

* Folder layout = **`Rename in place`**
* File name    = **`{cleaned source filename}.mp3 (no metadata)`**

In this mode Music Sorter:

* skips the entire identification pipeline (no MusicBrainz / AcoustID /
  Shazam calls — runs at disk speed)
* leaves every file in its current folder
* leaves ID3 tags and embedded cover art untouched
* just renames `Artist - Song (Official Music Video) [HD]_dQw4w9WgXcQ.mp3`
  to `Artist - Song.mp3`

`Move` vs `Copy` still applies — Move renames the file, Copy creates a
cleaned-name duplicate next to it.

### "Audio-only" recipes

* **Just renamed audio**, no metadata at all? Tags = `Strip all tags`,
  Art = `Strip all art`. Identification still computes the destination
  filename / folder, but the file ends up tag-free.
* **Strip embedded art only**, keep tags? Tags = `Full`, Art = `Strip`.
* **Fix bad/missing tags without touching good ones**? Tags =
  `Fill blanks only`, Art = `Keep existing`.

Settings (including API keys) are persisted to
`%APPDATA%\MusicSorter\settings.json` between runs.

## Smart App Control / SmartScreen blocked the .exe

Music Sorter is unsigned (no commercial code-signing certificate), so on
Windows 11 with **Smart App Control** (or any Windows with **SmartScreen**
turned on) you may see one of:

* "Microsoft Defender SmartScreen prevented an unrecognised app from
  starting"  →  click **More info**, then **Run anyway**.
* "Smart App Control blocked an app that may be unsafe"  →  this one is
  stricter. Three options:
  1. **Right-click `MusicSorter.exe` → Properties → check *Unblock* at
     the bottom → OK.** This sets the file's "downloaded from Internet"
     flag off; SAC then evaluates it locally instead of refusing
     outright.
  2. Build it yourself: clone the repo and run `build.cmd`. Files you
     compiled locally aren't flagged as "downloaded".
  3. Turn Smart App Control off in *Windows Security → App & browser
     control → Smart App Control settings*. Note that SAC can only be
     turned **back** on by reinstalling Windows, so this is a one-way
     door — only do it if you're comfortable with that trade-off.

The published binary includes a Windows version resource (Company,
Product, Copyright, FileVersion) and a side-by-side application
manifest declaring per-monitor DPI awareness, long-path support and
Windows 10/11 compatibility. That's the most a self-published .exe
can do without a signing certificate.

## Troubleshooting startup crashes

Music Sorter writes two diagnostic files into `%APPDATA%\MusicSorter\`
that should always be checked in this order:

1. **`startup.log`** — written by a module initializer the moment our
   assembly is loaded by the runtime. Each phase appends a line:

   ```
   12:01:02.103 module initializer reached
   12:01:02.110 App constructor entered
   12:01:02.111 App constructor finished
   12:01:02.114 OnStartup entered
   12:01:02.428 OnStartup base completed (window should be showing)
   ```

   * **No `startup.log` at all** → the assembly never loaded. The
     single-file extraction failed (antivirus, locked `%TEMP%`, etc.)
     or the runtime is incompatible. Try `build-folder.cmd` instead of
     `build.cmd` — that produces a folder publish with no extraction
     step. Run the `.exe` from inside `publish-folder\`.
   * **Only `module initializer reached`** → the App constructor never
     ran. Almost always a JIT failure on App's static refs.
   * **Stops at `OnStartup entered`** → the WPF MainWindow ctor or
     XAML parse threw. The exception itself is in `last-error.log`.

2. **`last-error.log`** — full exception chain (type, message, inner
   exception, every stack trace, OS / CLR / app-dir info) for any
   unhandled crash. Whenever you see the red popup, this file has the
   real reason.

If `%APPDATA%` isn't writable both files fall back to `%TEMP%\`
(`MusicSorter-startup.log` and `MusicSorter-last-error.log`).

### Build options

* `build.cmd` — single-file `publish\MusicSorter.exe`. Self-extracts
  on first run. Compression is **off** because it has caused first-run
  failures on some Windows configurations.
* `build-folder.cmd` — folder publish at `publish-folder\`. No
  extraction step at all; if single-file boot fails, this always
  works. The whole folder is needed — you can't move the `.exe` alone.

## How it decides what's "matched"

Each enrichment source returns a confidence in `[0..1]`. The pipeline merges
results, keeping the highest-confidence value for each field. A track is
considered matched if it ends with both an artist and a title and an overall
confidence ≥ 0.55 — otherwise the **letter-bucket fallback** kicks in
(see below) and the file lands at `<first-letter>/<cleaned filename>.mp3`
in the output. If you uncheck the fallback toggle, unmatched files go to
`_Unsorted/` instead, ready for manual review.

### Letter-bucket fallback (last resort)

When the entire identification cascade — ID3, filename parse, MusicBrainz,
AcoustID, Shazam — fails to produce a confident match, the file is bucketed
by the **first letter of its cleaned source filename**:

```
random YouTube weirdness.mp3   →   R / random YouTube weirdness.mp3
4 minutes mystery rip.mp3      →   0-9 / 4 minutes mystery rip.mp3
[unknown] track from 2003.mp3  →   # / unknown track from 2003.mp3
The Beatles weird mix.mp3      →   B / Beatles weird mix.mp3   (leading "The " stripped)
```

This way nothing ever just "disappears" into a single dumping ground —
every file ends up somewhere browsable. Toggle it off in *File operation
→ "If identification fails, bucket by first letter…"* if you'd rather
have the old `_Unsorted/` behaviour.

## Project layout

```
MusicSorter.sln
MusicSorter/
  MusicSorter.csproj          .NET 8 WPF, single-file publish, Win32 metadata
  app.manifest                DPI / long-paths / Windows 10-11 declarations
  App.xaml / App.xaml.cs      app shell, dark theme, fatal-error reporter
  MainWindow.xaml(.cs)        UI
  StartupTrace.cs             ModuleInitializer breadcrumbs (startup.log)
  Models/
    TrackMetadata.cs          merged metadata struct
    FolderLayout.cs           layout + filename pattern enums
    WriteOptions.cs           tag / art / dup mode enums + dropdown items
    SortOptions.cs            run options + AppSettings
  Services/
    FilenameCleaner.cs        regex-based YouTube-cruft stripping
    MusicBrainzClient.cs      ws/2 recording search
    ChromaprintRunner.cs      shells out to fpcalc.exe
    AcoustIdClient.cs         api.acoustid.org/v2/lookup
    ShazamClient.cs           RapidAPI Shazam search
    CoverArtClient.cs         coverartarchive.org / Shazam image
    Mp3TagService.cs          read/write ID3 with TagLibSharp
    AudioFile.cs              tag + properties + SHA-1 + quality compare (single TagLib pass)
    DuplicateIndex.cs         multi-key dedup index
    FolderCapTracker.cs       per-folder file cap with auto-overflow ('A (2)')
    RecycleBin.cs             SHFileOperation P/Invoke (delete to bin)
    PathBuilder.cs            destination path + safe segments + letter buckets
    EnrichmentPipeline.cs     orchestrates all sources
    Mp3Sorter.cs              top-level scan/move worker
    SettingsStore.cs          %APPDATA% JSON persistence
build.cmd                     single-file publish to publish\
build-folder.cmd              folder-based publish to publish-folder\ (fallback)
```

## Limitations / honest notes

* **MP3 only** for now (the project structure makes it trivial to add FLAC /
  M4A by relaxing the `*.mp3` glob and TagLib# already supports them).
* Shazam audio fingerprinting requires uploading a 16-bit/44.1 kHz mono PCM
  sample, which would mean bundling an MP3 decoder — Music Sorter currently
  uses Shazam's *text search* endpoint, which still gets you Shazam-quality
  album / genre / cover art for tracks where MusicBrainz is missing data.
* MusicBrainz throttles to ~1 request/second; the client respects that, so
  large libraries take a while.
* Files that fail to identify are not deleted — they go to `_Unsorted/` with
  the cleaned filename so a manual sweep is easy.
