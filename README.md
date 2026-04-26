# Music Sorter

A Windows WPF app that takes a folder of badly-named music (typical YouTube
rips: `Artist - Song (Official Music Video) [HD].mp3`) and rewrites it into a
clean library:

* strips YouTube cruft from filenames (`(Official Music Video)`,
  `[Lyrics]`, `(HD)`, trailing video IDs, uploader tags...)
* identifies each track using **every available method** in cascade:
  1. existing ID3 tags
  2. cleaned filename
  3. MusicBrainz title/artist search (free)
  4. AcoustID audio fingerprint (Chromaprint + AcoustID API key)
  5. Shazam search (RapidAPI key)
* downloads the cover art (Cover Art Archive or Shazam image)
* writes ID3v2 tags (artist, album, year, track #, genre, MBID, cover)
* moves (or copies) the file into a destination layout you pick at run-time
* anything that can't be confidently identified lands in `_Unsorted/`

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
  * `Genre / Artist / Album / <file>`
  * `Year / Artist / Album / <file>`
  * `Flat (no subfolders)`
* **File name** — pick from `01 - Title.mp3`, `Artist - Title.mp3`,
  `01 - Artist - Title.mp3`, or `Title.mp3`.
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

### "Audio-only" recipes

* **Just renamed audio**, no metadata at all? Tags = `Strip all tags`,
  Art = `Strip all art`. Identification still computes the destination
  filename / folder, but the file ends up tag-free.
* **Strip embedded art only**, keep tags? Tags = `Full`, Art = `Strip`.
* **Fix bad/missing tags without touching good ones**? Tags =
  `Fill blanks only`, Art = `Keep existing`.

Settings (including API keys) are persisted to
`%APPDATA%\MusicSorter\settings.json` between runs.

## How it decides what's "matched"

Each enrichment source returns a confidence in `[0..1]`. The pipeline merges
results, keeping the highest-confidence value for each field. A track is
considered matched if it ends with both an artist and a title and an overall
confidence ≥ 0.55 — otherwise it goes to `_Unsorted/` so you can review it
manually.

## Project layout

```
MusicSorter.sln
MusicSorter/
  MusicSorter.csproj          .NET 8 WPF, single-file publish
  App.xaml / App.xaml.cs      app shell, dark theme
  MainWindow.xaml(.cs)        UI
  Models/
    TrackMetadata.cs          merged metadata struct
    FolderLayout.cs           layout + filename pattern enums
    WriteOptions.cs           tag/art/dup mode enums + dropdown items
    SortOptions.cs            run options + AppSettings
  Services/
    FilenameCleaner.cs        regex-based YouTube-cruft stripping
    MusicBrainzClient.cs      ws/2 recording search
    ChromaprintRunner.cs      shells out to fpcalc.exe
    AcoustIdClient.cs         api.acoustid.org/v2/lookup
    ShazamClient.cs           RapidAPI Shazam search
    CoverArtClient.cs         coverartarchive.org / Shazam image
    Mp3TagService.cs          read/write ID3 with TagLibSharp
    AudioFile.cs              file properties + SHA-1 + quality compare
    DuplicateIndex.cs         multi-key dedup index
    RecycleBin.cs             SHFileOperation P/Invoke (delete to bin)
    PathBuilder.cs            destination path + safe segments
    EnrichmentPipeline.cs     orchestrates all sources
    Mp3Sorter.cs              top-level scan/move worker
    SettingsStore.cs          %APPDATA% JSON persistence
build.cmd                     one-shot Windows build script
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
