# Endfield Direct

[中文](README.md) | **English**

A small Windows tool for starting Endfield directly. Set the game location in `config.json` and launch without opening the GRYPHLINK launcher interface first.

Tested and confirmed to launch the game successfully for use. See [acceptance results](ACCEPTANCE.md) for test coverage.

> This is an unofficial tool. The game and official GRYPHLINK launcher must be installed separately.

## Features

- **Configurable paths**: one executable works across installation locations without source changes.
- **Launcher discovery**: finds `Games.exe` in GRYPHLINK launcher version directories, with an explicit path option.
- **Duplicate prevention**: avoids starting another game when a game or tool instance is already running.
- **Automatic cleanup**: removes the suspended parent created by the tool when the game exits.
- No game-file patches, service installation, or automatic elevation.

## Requirements

- Windows 10/11 x64.
- .NET Framework 4.x; building uses Windows PowerShell 5.1 and the x64 C# compiler.
- Installed Endfield and the official GRYPHLINK launcher.
- Run as administrator if the official executables require elevation.

## Quick start

### 1. Download

Download `Endfield-Direct.exe` and `config.example.json` from [Releases](https://github.com/Willy19928/endfield-direct/releases/latest) and place them in the same folder. Use `SHA256SUMS` on the release page to verify the files.

### 2. Configure paths

Copy `config.example.json` to `config.json` beside the executable and set the game location:

```json
{
  "gamePath": "D:\\Games\\Endfield\\Endfield.exe",
  "launcherPath": ""
}
```

| Field | Purpose |
|---|---|
| `gamePath` | Required; points to `Endfield.exe` |
| `launcherPath` | Empty or omitted for discovery, or an explicit path to official `Games.exe` |

Use `/` or JSON-escaped `\\` in paths. Relative paths resolve against the **tool executable's directory**. After moving the game, edit the configuration; rebuilding is unnecessary. The tool never overwrites the configuration.

### 3. Launch

Run `Endfield-Direct.exe`. If Windows reports that elevation is required, right-click it and select **Run as administrator**.

## Commands

| Argument | Purpose |
|---|---|
| None | Start the game using the configuration |
| `--check-config` | Check configuration and file paths without creating game or launcher processes |
| `--self-test` | Check Job cleanup with suspended test processes; does not execute the game |

To read the result in PowerShell:

```powershell
$p = Start-Process .\Endfield-Direct.exe -ArgumentList '--check-config' -PassThru -Wait
$p.ExitCode
```

Exit codes: `0` success, `1` error, `2` failed process self-test, `3` another tool instance is running.

Normal launch errors appear in a message box. Diagnostic modes attempt to write `Endfield-Direct-error.txt` beside the executable on failure. Successful runs do not remove old error files; use the current exit code. Application messages are in Traditional Chinese; Windows supplies system error messages.

## How it works

The tool creates official `Games.exe` suspended and designates it as the game's parent. After checking Job isolation, it resumes the game. The official parent remains suspended until cleanup when the game exits.

Official files are not modified, and the launcher's normal interface flow is not executed. See [implementation notes](docs/implementation.md) for process-management details.

## Development and testing

Run from the project root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

The build runs configuration tests and writes the executable, example configuration, and `SHA256SUMS` to `dist/` without starting the game.

To test process cleanup, prepare `dist/config.json` and run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -SelfTest
```

The process test requires the official launcher and any privileges it requires. See [ACCEPTANCE.md](ACCEPTANCE.md) for test coverage.

### Project layout

| Path | Purpose |
|---|---|
| `src/EndfieldDirect.cs` | Launching, process cleanup, and diagnostics |
| `src/AppConfig.cs` | Configuration and path validation |
| `src/app.manifest` | `asInvoker` caller-privilege policy |
| `scripts/build.ps1` | Tests, compilation, and SHA-256 output |
| `tests/ConfigTests.cs` | Configuration regression tests |

User configuration, diagnostic files, and build output are excluded from version control.

## Known limitations

- Game updates may change launch verification; compatibility with subsequent updates requires separate confirmation.
- Launch arguments are fixed to `-launcher_lang=zh-tw -launcher_sub_channel=6`; other argument combinations have not been tested.
- Launcher discovery selects a numeric version directory; the tool does not verify signatures at runtime.

## License

This project is licensed under the GNU General Public License v3.0 (GPL-3.0-only). See [LICENSE](LICENSE).
