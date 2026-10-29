# RoboDock

**A GUI for robocopy. No flags to memorize, and you always know before it deletes anything.**

English | [简体中文](README.md)

> 🚧 Work in progress (M1). UI and wording may change. Feature requests are welcome in Issues.

---

## Why

robocopy ships with Windows and is one of the fastest copy tools available —
especially with **many small files**, where it easily beats File Explorer.
It also does multi-threaded copies, restartable transfers, and reliable logs.

But it has three problems that keep people away:

| Problem | What it looks like |
|---|---|
| **Too many flags** | 80+ switches; `/XO`, `/XN`, `/XC` are indistinguishable |
| **Counter-intuitive exit codes** | Exit code `1` means **success**; only `<8` is success |
| **`/MIR` deletes data** | Mirror mode removes extra files at the destination, silently |

Most existing GUIs stop at "turn this form into a command line". None of them fix those three.

RoboDock fixes the three first — features come second.

## Three design principles

1. **Usable without reading docs** — Simple mode asks three questions
2. **Dangerous operations must be previewed** — you always see "will delete N files" first
3. **Results in plain language** — copied / skipped / failed / deleted, with exit codes tucked into details

## Quick start

1. Download `RoboDock.zip` from Releases and extract it anywhere (portable)
2. Run `RoboDock.exe`
3. Pick a source folder, a destination folder, a strategy → **Preview** → **Start copy**

<!-- TODO(M1): screenshot -->

## The four strategies

Only the second one deletes anything.

| Name in the app | Flags | Deletes at destination? | Use when |
|---|---|---|---|
| 📄 Backup over | `/E` | ❌ No | Additive copy, nothing removed |
| 🔁 Keep identical | `/MIR` | ✅ **Yes** | Destination must match source exactly |
| ⬆️ Update changed only | `/E /XO` | ❌ No | Overwrite older files, keep extras |
| ➕ Fill missing only | `/E /XC /XN /XO` | ❌ No | Never overwrite anything existing |

## Why it's faster than File Explorer

- **Multi-threaded** (`/MT:32`) — small files copied in parallel
- **Incremental** — timestamp/size comparison, no redundant copying
- **Restartable** (`/Z`) — resume interrupted large files
- **Unbuffered I/O** (`/J`) — for multi-GB single files
- **No UI overhead** — no per-file progress animation

<!-- TODO(M2): benchmark numbers vs File Explorer -->

## Common recipes

- **Back up photos/videos to a USB drive** — use "Backup over"
- **Mirror a project to a NAS** — use "Keep identical"; extras on the NAS get deleted (the app shows the count first)
- **Move huge files to an external drive** — enable restartable + unbuffered I/O, `/MT:8`
- **Bulk-copy many small files** — set `/MT:32`; use `/R:0` so failures don't stall

## ⚠️ About destructive operations

`/MIR` and `/PURGE` delete files at the destination. robocopy does not use the Recycle Bin.

RoboDock applies four safeguards:

1. Red warning on the strategy card; the action button becomes "Confirm mirror & delete"
2. **Preview is mandatory** — the "delete N files" line is shown separately in red
3. A second confirmation dialog before execution
4. Logging is forced on; the deletion list is written to disk

## Exit codes

robocopy exit codes are a **bitmask**. Anything `< 8` is success.

| Code | Meaning |
|---|---|
| 0 | Nothing to copy, no failure |
| 1 | **Files copied successfully** (the common, good case) |
| 2 | Extra files/directories detected at destination |
| 3 | Files copied + extras present |
| 4 | Mismatched files/directories detected |
| 5 | Copied + mismatches |
| 6 | Extras + mismatches |
| 7 | 1+2+4 |
| **8** | **Some files could not be copied** |
| 9–15 | 8 + the bits above |
| **16** | **Fatal error; nothing copied** |

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/GCSFML/RoboDock.git
cd RoboDock
dotnet build
dotnet run --project src/RoboDock.App

# Run the tests (the test project has zero NuGet dependencies, works offline)
dotnet run --project tests/RoboDock.Core.Tests
```

Publishing:

```powershell
# Framework-dependent (smaller)
dotnet publish src/RoboDock.App -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# Self-contained (no runtime needed, ~70 MB)
dotnet publish src/RoboDock.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Project layout

```
RoboDock/
├─ src/
│  ├─ RoboDock.Core/          # Pure logic, no UI, fully unit-testable
│  │  ├─ CopyStrategy.cs      # Strategies → switch mapping
│  │  ├─ RobocopyOptions.cs   # Typed options model
│  │  ├─ CommandBuilder.cs    # Builds the command line
│  │  ├─ PreviewParser.cs     # Parses /L output (locale independent)
│  │  ├─ SummaryParser.cs     # Parses the summary table (locale independent)
│  │  ├─ ExitCodeTranslator.cs
│  │  ├─ PathValidator.cs
│  │  └─ RobocopyRunner.cs    # Process handling + encoding detection
│  └─ RoboDock.App/           # WPF UI
├─ tests/RoboDock.Core.Tests/ # Zero-dependency test runner
├─ docs/
└─ .github/workflows/         # CI: build + test + release
```

### One implementation gotcha worth knowing

robocopy's UI language **follows the console code page**: the same binary prints
`Files :` under an English console and `文件:` under a Chinese one. So the parsers
never match labels — they rely on structure only. The summary block is always four
rows (dirs / files / bytes / times) with six numeric columns, and in `/L` output any
item to be deleted has a class column starting with `*`, while directories always
end their path with `\`.

## Requirements

- Windows 10 / 11 (`C:\Windows\System32\Robocopy.exe` is built in)
- .NET 10 Desktop Runtime (not needed with the self-contained build)

## FAQ

**Can I stop it halfway?**
Yes. Completed files stay; the next run skips them. Restartable mode resumes large files.

**Will it overwrite files?**
Depends on the strategy. "Backup over" overwrites older files; "Fill missing only" never overwrites.

**Can deleted files be recovered?**
No. robocopy bypasses the Recycle Bin — which is exactly why `/MIR` requires a preview.

**Does it need admin rights?**
Usually not. Only backup mode (`/B`) needs elevation.

**Does it phone home?**
No. No telemetry, no uploads, fully local.

## Contributing

Issues and PRs welcome. Please run `dotnet test` before submitting.

## License

[MIT](LICENSE)
