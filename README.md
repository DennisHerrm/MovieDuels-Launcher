# MovieDuels Launcher

The launcher for [Star Wars: Movie Duels](https://www.moddb.com/mods/movie-duels): one window to start the
singleplayer or the multiplayer game, set a few options and reach the mod's pages.

It is a WPF application for .NET Framework 4.8, which is part of Windows 10 and 11, so players need to
install nothing. It builds to a single file, `MovieDuels-Launcher.exe`, with all art and sound inside it.

## Using it

Put `MovieDuels-Launcher.exe` in the MovieDuels game folder, next to `MovieDuels-SP.x86_64.exe` and
`MovieDuels-MP.x86_64.exe`.

| Button | What it does |
| --- | --- |
| SINGLEPLAYER MODE | starts `MovieDuels-SP.x86_64.exe` and closes the launcher |
| MULTIPLAYER MODE | starts `MovieDuels-MP.x86_64.exe` and closes the launcher |
| Gear | the options page: Renderer (Vanilla / Rend2), Display (Fullscreen / Windowed), Animations (Classic / Master, singleplayer only) |
| ModDB, HELP, Discord, YouTube, JKHub, Nexus | open the mod's pages in the browser |
| Speaker, –, × | music on / off, minimise, close (Esc closes too) |

The options are passed to the game as `+set` values (`cl_renderer`, `com_rend2`, `r_fullscreen`,
`g_ActivateAnimationStyle`) and remembered in `Documents\My Games\MovieDuels\MovieDuels-Launcher.ini`.

## Building

Open `MovieDuels-Launcher.sln` in Visual Studio (with the ".NET desktop development" workload) and build,
or from a Developer Command Prompt:

```
msbuild MovieDuels-Launcher.sln -p:Configuration=Release
```

The exe is written to `MovieDuels-Launcher\bin\Release`.

## Changing it

- **Art and sound**: `MovieDuels-Launcher\Assets`. The images are the design's 1920x1080 layers, stacked in
  `MainWindow.xaml`; replace a file (same name) and rebuild. `Download Button.png` and
  `Download Ready Button.png` are not used yet.
- **Layout, colours, link addresses**: `MainWindow.xaml`. Each link button's address is its `Tag`.
- **Game exe names, command line, options**: `MainWindow.xaml.cs` and `LauncherSettings.cs`.
- The design's Background Panel is glass (81% opaque). The window is made solid by the black `Border`
  under it in `MainWindow.xaml`; remove that one element for a see-through window.
