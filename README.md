# Clicky for Windows

Clicky for Windows is an open-source Windows adaptation of the HeyClicky AI desktop companion. It can look at your screen when asked, answer typed or spoken questions, point to visible interface elements, and help with a small set of Windows-specific tasks.

## Project lineage and credits

This project exists because of two earlier projects:

- [Farzaa](https://github.com/farzaa) created the original [Clicky for macOS](https://github.com/farzaa/clicky), including the core idea of a small AI cursor companion that can see and discuss the screen.
- [emreyilmaz46](https://github.com/emreyilmaz46) created the original [.NET/WPF Windows port](https://github.com/emreyilmaz46/clicky_windows). This repository was cloned from that implementation and continues to build on its Windows foundation.

The features listed below are additions and refinements made in this fork. Credit for the original concept and upstream implementations remains with their respective creators. This fork is an independent community project and does not imply their endorsement.

## What changed in this fork

Compared with the original Windows port, this version adds or substantially updates:

- A compact glass-style chat panel positioned near the edge of the desktop
- Typed prompts with Enter-to-send and Shift+Enter for a new line
- Adaptive panel sizing for short and long answers
- A setup and settings window, onboarding flow, and system-tray controls
- Gemini support alongside the original Anthropic Claude provider
- English interface text and clearer provider error handling
- A redesigned blue Clicky cursor and updated application icon
- Point & Guide responses that can move Clicky toward an on-screen target
- Smooth target movement that waits for the user to approach before returning
- Multi-monitor and Windows DPI-aware coordinate conversion
- Explicit click and double-click actions requested through the AI response
- Two-step commands that open an allowlisted Windows app, recapture its window, and click one requested visible control
- An instant local shortcut for locating the MediHacks desktop folder without an AI request
- Safe launching of common Windows apps such as Chrome, Edge, Notepad, Explorer, and Settings
- A screen-area selection tool for sending only a chosen region to the AI
- Focused Windows UI Rescue tools for a missing or frozen taskbar and disappeared desktop icons
- Improved startup behavior, single-instance handling, and append-only diagnostic logging

This is intentionally not a universal Windows repair utility. The troubleshooting features stay focused on common Windows shell and desktop UI problems.

## Main features

### Ask about your screen

Open the compact chat panel, type a question, and Clicky captures the visible display as context for the configured AI provider.

### Point & Guide

The AI can return a hidden instruction in this form:

```text
[POINT: 520, 1040, "Color Page Button"]
```

Clicky removes the instruction from the displayed answer, converts the coordinates for the current Windows display layout, and smoothly moves the blue pointer to the target. The pointer remains there until your real mouse reaches it.

Explicit `CLICK` and `DOUBLE_CLICK` instructions are also supported. These actions are only intended to run when the user's request clearly asks Clicky to interact with something.

### Select part of the screen

Selection mode dims the current display and lets you drag a clear crop rectangle around the exact area you want to discuss. Press Escape to cancel safely.

### Voice questions

Hold the voice shortcut, speak, and release it to submit. AssemblyAI handles transcription. ElevenLabs speech output is optional when configured.

### Windows UI Rescue

The app includes two deliberately narrow troubleshooting flows:

- Missing or non-responsive Windows taskbar
- Desktop icons that have disappeared

The flows explain what is happening and guide the user through Windows-specific recovery steps.

## Keyboard shortcuts

| Action | Shortcut |
| --- | --- |
| Show or hide typed chat | `Ctrl+Shift+K` |
| Select a screen region | `Ctrl+Shift+S` |
| Hold to ask by voice | `Ctrl+Shift+Space` |
| Submit a typed prompt | `Enter` |
| Add a line inside the prompt | `Shift+Enter` |
| Cancel screen selection | `Escape` |

The same commands are available from the Clicky system-tray menu.

## Requirements

- Windows 10 or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A Gemini API key or Anthropic API key for screen questions
- An AssemblyAI API key for voice transcription
- An ElevenLabs API key only if spoken responses are enabled

## Run from source

Open PowerShell in the repository folder and run:

```powershell
cd clicky_windows
dotnet restore
dotnet run
```

On first launch, use the setup window to choose an AI provider and enter the required API keys. Clicky continues running in the Windows notification area when its main panel is hidden.

## Build a release

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The published executable is created under:

```text
bin/Release/net8.0-windows/win-x64/publish/
```

## Configuration and logs

Clicky stores its local settings and diagnostic log under:

```text
%APPDATA%\ClickyWindows\
```

API keys should never be committed to Git. The repository's example settings file contains placeholders only.

When you ask a screen-based question, the selected screenshot is sent to the AI provider you configured. Review that provider's privacy terms before using Clicky with sensitive information on screen.

## Technology

- C# and .NET 8
- WPF desktop UI
- Windows global hotkeys and native interop
- Gemini or Anthropic for visual reasoning
- AssemblyAI for speech-to-text
- ElevenLabs for optional text-to-speech

## License

This project is distributed under the MIT License. See [LICENSE](LICENSE) for the full terms. Please preserve the upstream copyright and attribution notices when redistributing a modified version.
