# Ternion Overlay for TeamSpeak 6

**English** · [Русский](README.ru.md)

A **TeamSpeak 6 voice chat overlay for Windows 10/11 x64** (TS6) that displays the people in your current voice channel over desktop windows and games. It connects to the **TeamSpeak client running on the same PC** through Remote Apps—not directly to the voice server. You do not need a server administrator account or a separate bot.

> This project is still being tested. Whether it appears over a full-screen game depends on the game's display mode and Windows. Try a windowed or borderless game first. This is not an official TeamSpeak app.

## Features

- A separate dark, rounded capsule for each participant in your current channel. The capsule grows to fit the name and avatar. New arrivals appear without restarting the app: their capsule briefly flashes blue, then returns to gray.
- A speaking participant gets a green name and outline. Separate red icons indicate a muted microphone and muted audio.
- Show the nickname alone or `nickname (name from Description)` when the participant has filled in their Description. No name is guessed or entered by the overlay.
- Show a myTeamSpeak avatar (when available) on either side. No placeholder avatar is shown when one is unavailable.
- Show everyone or only speakers; align capsules to the left or right; adjust font size and minimum panel width.
- Transparent space between capsules, optional click-through, lock/unlock for dragging, and show/hide from the tray or with `Alt+T`.
- English and Russian interface, configurable TS6 port, and optional Windows auto-start.

## How to show players' real names

**Each player whose name you want to see must fill in the Description field for their own client in TeamSpeak.** The overlay does not get names from Steam or contacts, and it cannot infer someone else's name. An empty Description means only the nickname is shown. Other players do **not** need to install the overlay; only the person viewing it does.

Instructions for **each** player:

1. Connect to a voice server in TeamSpeak 6.
2. Click **your own nickname** in the client list in the voice channel, then the **pencil/edit icon** for client settings.
3. Enter the name you want shown (for example, `Alex`) in **Description**. This is the **client's Description**, not the server or channel description.
4. Save the change with **Done** (or the corresponding button in your client language).
  
On the PC running the overlay, open **Settings → Names** and select **Nick + real name from description**. The player will then appear as `Nickname (Alex)`. If **Nickname only** is selected, Description is hidden even if it is filled in. If a recently edited name does not appear, confirm it was saved in TeamSpeak and reconnect to the voice server to refresh the data.

## Getting started

**Download for Windows (v1.0):** [TernionOverlay-v1.0-win-x64.exe](https://github.com/eliora90/ternion-voice-overlay-TeamSpeak-6/releases/download/v1.0/TernionOverlay-v1.0-win-x64.exe) ([release notes](https://github.com/eliora90/ternion-voice-overlay-TeamSpeak-6/releases/tag/v1.0)). This build includes the owner-approved settings design; behavior in individual games and display modes may vary. You can also build from source below.

1. Install and open the **TeamSpeak 6 client** on the **same Windows PC** that will run the overlay. Connect to your voice server and join a channel. No TeamSpeak server-side configuration is required.
2. In TeamSpeak, open **Settings → Remote Apps** and make sure access is enabled. Check the local port: the overlay initially uses **5899**. If another Remote App (for example, Stream Deck) already uses a different port, configure the overlay to use **that same TS6 port** rather than changing the other app.
<img width="1722" height="944" alt="Снимок экрана 2026-10-16 164035" src="https://github.com/user-attachments/assets/5a3d688b-4125-4adf-b843-ec4d13c46958" />



3. Download the Windows EXE above, or build it using the instructions below. Place it in a permanent directory, such as `C:\Apps\TernionOverlay\`. A self-contained build does not require a separate .NET installation on the Windows PC.
4. Run the EXE. On first connection, TeamSpeak may ask you to authorize **Ternion Overlay**; allow it in the client. The app will appear in the authorized-apps list, and later launches normally do not prompt again. Its authorization key is stored locally in your Windows profile.
5. If the panel does not appear or shows a status message, check that TS6 is running; the **ports match**; access was authorized; and the TS6 client is **connected to a voice server and in a channel**. App authorization and connection to the voice server are separate conditions.
6. Find the Ternion icon in the system tray (expand hidden icons if necessary). Open **Settings** to choose the language, alignment, name mode, avatars and other options. To reposition the overlay, choose **Move overlay (unlock)** from the tray, drag a visible capsule, then choose **Lock overlay (click-through)**. When locked, clicks pass through to the game if click-through is enabled.

To see other players' names, ask them to follow the [Description instructions](#how-to-show-players-real-names).

**Controls:** `Alt+T` shows/hides the panel; double-clicking the tray icon does the same. Select **Exit** in the tray to quit completely. Before replacing the EXE with a newer version, quit the older instance through the tray.
<img width="756" height="978" alt="Снимок экрана 2026-10-16 163048" src="https://github.com/user-attachments/assets/5bca250d-9728-4998-9929-bf1328c9d748" />



### Local data

Settings are stored in `%AppData%\TernionOverlay\config.json`, the local authorization key in `%AppData%\TernionOverlay\apikey.txt`, and avatar cache files in `%AppData%\TernionOverlay\avatars\`. These files are **not uploaded to the repository** and should not be shared. Deleting only the EXE does not reset your settings.

## Building from source

You need the **.NET 8 SDK**. From the repository root:

```powershell
dotnet publish src/TernionOverlay/TernionOverlay.csproj -c Release -r win-x64 --self-contained true -o publish
```

The resulting file is `publish\TernionOverlay.exe`. This is a WinForms application. `EnableWindowsTargeting` allows a Linux build, but **the UI and gameplay behavior must be checked on Windows**. In Visual Studio, open `src/TernionOverlay/TernionOverlay.csproj`.

To test the protocol against a local TeamSpeak mock instead of a real client:

```powershell
dotnet run --project src/TernionHarness/TernionHarness.csproj -c Release
```

The harness tests the connection snapshot, Description, channel membership and events. It **does not validate the WinForms UI**. Project layout: `TernionCore` contains models and the Remote Apps client; `TernionOverlay` contains the overlay window, settings and tray; `TernionHarness` contains a local test WebSocket server.

## Troubleshooting

- **“Waiting for TeamSpeak”** — TS6 must run on the **same PC**, Remote Apps must be enabled, and both ports must match. It connects over `127.0.0.1`; no port forwarding on the TeamSpeak server is needed.
- **“Waiting for permission”** — approve the request in TS6. If the app is already authorized, check that TS6 itself is connected to a voice server; there may be no new permission request.
- **“Connect to a server” / empty list** — join the voice server and the intended channel in TS6. The overlay retries its connection to obtain a channel snapshot after you join.
- **Name is clipped** — check minimum panel width, font size and Windows scaling. The window expands for names and loaded avatars, up to the screen width.
- **Not visible over a game** — first test it on the desktop, then try a windowed or borderless game. Ordinary windows may not appear over every exclusive full-screen mode.
- **Cannot drag it** — unlock via the tray and drag a visible capsule rather than the transparent gap; lock it again after moving.

## Future plans

Design updates and collecting feedback.

## Made with vibe coding through Hermes Agent (OpenAI ChatGPT 6 Sol + DeepSeek v4 Flash 0713)

## Status and distribution

`dist/`, `bin/`, `obj/` and user configuration are excluded from Git; the v1.0 EXE is distributed as a release asset.

## Privacy notes

- The overlay talks only to the TeamSpeak client on `127.0.0.1` (the port you configure) and sends no telemetry.
- The only outbound request is downloading a participant's myTeamSpeak avatar. Avatars are limited to ordinary `http`/`https` addresses and to 4 MB, and are cached under `%AppData%\TernionOverlay\avatars\`.
- The TeamSpeak authorization key is stored unencrypted in `%AppData%\TernionOverlay\apikey.txt` and is only sent to your local TeamSpeak client. Do not share that file.
- Enabling auto-start writes the executable path to `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`. Disable the option to remove it.
- The overlay reads only the TeamSpeak data an authorized Remote Apps connection provides; it does not read game memory, inject into games, or capture your screen.

## License

Released under the [MIT License](LICENSE).
