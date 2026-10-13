using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Resources;

namespace MovieDuelsLauncher
{
    // The launcher window (layout and looks: MainWindow.xaml).
    //
    // - SINGLEPLAYER MODE starts MovieDuels-SP.x86_64.exe from the launcher's folder, passing the OPTIONS as "+set"
    //   values, and closes the launcher. MULTIPLAYER MODE opens the MULTIPLAYER page on the info card:
    //   START MULTIPLAYER starts MovieDuels-MP.x86_64.exe the same way, START SERVER runs
    //   "Start MovieDuels-MP-FFA-Server.bat" from the MD folder in its own console (the launcher stays open, so the
    //   game can be started next to join it). Multiplayer needs Update 9: on Update 8 the button only says it is
    //   coming soon.
    // - The gear opens the OPTIONS page on the info card: Renderer, Display, Resolution (read from and written to the game's own configs) and Controller (remembered by
    //   LauncherSettings). Which update the game's assets are is read from them the way the game does it
    //   (MdUpdate).
    // - The other icons and HELP open web pages in the browser (each button's Tag in the XAML).
    public partial class MainWindow : Window
    {
        private const string SPExe = "MovieDuels-SP.x86_64.exe";
        private const string MPExe = "MovieDuels-MP.x86_64.exe";

        // the mod folder (fs_game) next to the exes, and the dedicated server batch file in it (it does "cd .." itself, so
        // it runs from the mod folder)
        private const string ModFolder = "MD";

        private const string ServerBat = "Start MovieDuels-MP-FFA-Server.bat";

        // Release builds of the game only start when this is set on their process (shared/sys/sys_main.cpp
        // Sys_RequireLauncher: MD_LAUNCHER_ENV / MD_LAUNCHER_TOKEN); keep the two the same.
        private const string LauncherEnv = "MOVIEDUELS_LAUNCHER";

        private const string LauncherToken = "md-launcher-7f3a9c21";

        private const double MusicVolume = 0.6;
        private static readonly TimeSpan FadeTime = TimeSpan.FromMilliseconds(180); // the glow layers

        private readonly string folder; // where the launcher (and the game exes) are
        private readonly LauncherSettings settings;
        private readonly int mdUpdate;  // the MovieDuels update of the game's assets (8, 9, ...)

        // multiplayer is played with Update 9 and later
        private bool HasMultiplayer => mdUpdate >= 9;

        // menu sounds (the in-game menu's own); a newer one cuts off the one still playing
        private readonly SoundPlayer clickSound;

        private readonly SoundPlayer hoverSound;
        private readonly SoundPlayer backSound;

        // background music: MediaPlayer can only play a file, so the MP3 is written to %TEMP% first
        private MediaPlayer music;

        private string musicFile;

        public MainWindow()
        {
            folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            settings = new LauncherSettings(folder);
            mdUpdate = MdUpdate.Detect(folder);

            InitializeComponent();

            VersionText.Text = "Update " + mdUpdate;
            BuildText.Text = AutoVersion.VersionString;

            clickSound = LoadSound("Assets/Audio/button1.wav");
            hoverSound = LoadSound("Assets/Audio/menuroam.wav");
            backSound = LoadSound("Assets/Audio/esc.wav");

            ShowOptions();
            ShowMusicButton();
            Loaded += (sender, e) => StartMusic();
        }

        // ------------------------------------------------------------------------------------------
        // Sound
        // ------------------------------------------------------------------------------------------

        private static Stream OpenResource(string path)
        {
            StreamResourceInfo info = Application.GetResourceStream(new Uri(path, UriKind.Relative));
            return info?.Stream;
        }

        private static SoundPlayer LoadSound(string path)
        {
            try
            {
                var player = new SoundPlayer(OpenResource(path));
                player.Load();
                return player;
            }
            catch (Exception)
            {
                return null; // no sound device or a broken file: just no sound
            }
        }

        // wait: play it to the end first (for a click right before the launcher closes)
        private static void PlaySfx(SoundPlayer sound, bool wait = false)
        {
            try
            {
                if (wait)
                {
                    sound?.PlaySync();
                }
                else
                {
                    sound?.Play();
                }
            }
            catch (Exception)
            {
            }
        }

        private void StartMusic()
        {
            if (music != null || !settings.Music)
            {
                return;
            }
            try
            {
                if (musicFile == null)
                {
                    string file = Path.Combine(Path.GetTempPath(), "MovieDuels-Launcher-music.mp3");
                    using (Stream source = OpenResource("Assets/Audio/Music.mp3"))
                    {
                        // reuse the file of an earlier run (or of a second launcher that is playing it)
                        if (!File.Exists(file) || new FileInfo(file).Length != source.Length)
                        {
                            using (FileStream target = File.Create(file))
                            {
                                source.CopyTo(target);
                            }
                        }
                    }
                    musicFile = file;
                }
                music = new MediaPlayer { Volume = MusicVolume };
                music.MediaEnded += (sender, e) =>
                {
                    // the music reached its end: play it again (loop)
                    music.Position = TimeSpan.Zero;
                    music.Play();
                };
                music.Open(new Uri(musicFile));
                music.Play();
            }
            catch (Exception)
            {
                music = null; // no MP3 playback on this PC: just no music
            }
        }

        private void StopMusic()
        {
            if (music != null)
            {
                music.Stop();
                music.Close();
                music = null;
            }
        }

        // ------------------------------------------------------------------------------------------
        // Options page
        // ------------------------------------------------------------------------------------------

        private void ShowOptions()
        {
            RendererButton.Content = settings.Rend2 ? "RENDERER: REND2" : "RENDERER: VANILLA";
            DisplayButton.Content = settings.Windowed ? "DISPLAY: WINDOWED" : "DISPLAY: FULLSCREEN";
            ResolutionButton.Content = "RESOLUTION: " + settings.ResolutionText;
            ControllerButton.Content = settings.Controller ? "CONTROLLER: ON" : "CONTROLLER: OFF";
        }

        private void ShowMusicButton()
        {
            // speaker: waves when the music is on, a cross when it is off
            MusicOnMark.Visibility = settings.Music ? Visibility.Visible : Visibility.Collapsed;
            MusicOffMark.Visibility = settings.Music ? Visibility.Collapsed : Visibility.Visible;
        }

        private bool OptionsOpen => OptionsPage.Visibility == Visibility.Visible;
        private bool MultiplayerOpen => MultiplayerPage.Visibility == Visibility.Visible;
        private bool CardOpen => OptionsOpen || MultiplayerOpen; // a page on the info card covers the main page

        private void SetOptionsOpen(bool open)
        {
            OptionsPage.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            MainPage.IsHitTestVisible = !CardOpen; // the main page's buttons are covered by the options page
            UpdateGlow();
        }

        private void SetMultiplayerOpen(bool open)
        {
            MultiplayerPage.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            MainPage.IsHitTestVisible = !CardOpen; // the main page's buttons are covered by the multiplayer page
            UpdateGlow();
        }

        private void Options_Click(object sender, RoutedEventArgs e)
        {
            PlaySfx(clickSound);
            SetOptionsOpen(true);
        }

        private void OptionsBack_Click(object sender, RoutedEventArgs e)
        {
            PlaySfx(backSound);
            SetOptionsOpen(false);
        }

        private void Renderer_Click(object sender, RoutedEventArgs e)
        {
            settings.Rend2 = !settings.Rend2;
            OptionChanged(true);
        }

        private void Display_Click(object sender, RoutedEventArgs e)
        {
            settings.Windowed = !settings.Windowed;
            OptionChanged(true);
        }

        private void Resolution_Click(object sender, RoutedEventArgs e)
        {
            settings.NextResolution();
            OptionChanged(true);
        }

        private void Controller_Click(object sender, RoutedEventArgs e)
        {
            settings.Controller = !settings.Controller;
            OptionChanged();
        }

        private void OptionChanged(bool gameConfig = false)
        {
            PlaySfx(clickSound);
            settings.Save();
            if (gameConfig)
            {
                settings.SaveGameConfig(); // renderer, display, resolution: into the game's configs
            }
            ShowOptions();
        }

        // ------------------------------------------------------------------------------------------
        // Starting the game, links
        // ------------------------------------------------------------------------------------------

        private bool StartGame(bool multiplayer)
        {
            string exe = Path.Combine(folder, multiplayer ? MPExe : SPExe);
            if (!File.Exists(exe))
            {
                MessageBox.Show(this, "Could not find " + exe + "\n\nThe launcher must be in the MovieDuels game folder.",
                    "MovieDuels", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // "+set" on the command line overrides the values in the configs (they are the configs' own values unless the
            // OPTIONS page changed them, and then it wrote them into the configs too)
            string args = "";
            foreach (var kv in settings.GameConfigValues(multiplayer))
            {
                args += (args.Length > 0 ? " " : "") + "+set " + kv.Key + " " + kv.Value;
            }
            args += settings.Controller ? " +set in_joystick 1" : " +set in_joystick 0";

            try
            {
                var startInfo = new ProcessStartInfo(exe, args) { WorkingDirectory = folder, UseShellExecute = false };
                startInfo.EnvironmentVariables[LauncherEnv] = LauncherToken; // lets a release build of the game start
                Process.Start(startInfo)?.Dispose();
                return true;
            }
            catch (Exception)
            {
                MessageBox.Show(this, "Could not start " + exe, "MovieDuels", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void Play(bool multiplayer)
        {
            PlaySfx(clickSound, true); // the launcher closes right after
            if (StartGame(multiplayer))
            {
                Close();
            }
        }

        private void Singleplayer_Click(object sender, RoutedEventArgs e)
        {
            Play(false);
        }

        private void Multiplayer_Click(object sender, RoutedEventArgs e)
        {
            PlaySfx(clickSound);
            if (!HasMultiplayer)
            {
                MessageBox.Show(this, "Multiplayer is not available yet for Update " + mdUpdate + ".\n\nComing soon!",
                    "MovieDuels", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SetMultiplayerOpen(true);
        }

        private void StartMultiplayer_Click(object sender, RoutedEventArgs e)
        {
            Play(true);
        }

        private void StartServer_Click(object sender, RoutedEventArgs e)
        {
            PlaySfx(clickSound);
            StartServer(); // the launcher stays open, to start the game and join
        }

        private void MultiplayerBack_Click(object sender, RoutedEventArgs e)
        {
            PlaySfx(backSound);
            SetMultiplayerOpen(false);
        }

        // Runs the FFA dedicated server batch file from the MD folder in a console window of its own. The batch file
        // restarts the server when it crashes; closing its window stops it.
        private bool StartServer()
        {
            string modFolder = Path.Combine(folder, ModFolder);
            string bat = Path.Combine(modFolder, ServerBat);
            if (!File.Exists(bat))
            {
                MessageBox.Show(this, "Could not find " + bat + "\n\nThe launcher must be in the MovieDuels game folder.",
                    "MovieDuels", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            string comspec = Environment.GetEnvironmentVariable("ComSpec");
            if (string.IsNullOrEmpty(comspec))
            {
                comspec = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            }

            try
            {
                // cmd /c ""<bat>"": the outer quotes are cmd's, the inner ones keep the spaces in the path. Started from
                // a window program without a console, cmd gets a console window of its own.
                var startInfo = new ProcessStartInfo(comspec, "/c \"\"" + bat + "\"\"")
                {
                    WorkingDirectory = modFolder,
                    UseShellExecute = false
                };
                startInfo.EnvironmentVariables[LauncherEnv] = LauncherToken; // the server inherits it, as the game does
                Process.Start(startInfo)?.Dispose();
                return true;
            }
            catch (Exception)
            {
                MessageBox.Show(this, "Could not start " + bat, "MovieDuels", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // A link button: the web address is the button's Tag.
        private void Link_Click(object sender, RoutedEventArgs e)
        {
            string url = (sender as FrameworkElement)?.Tag as string;
            if (string.IsNullOrEmpty(url))
            {
                return; // link not set
            }
            PlaySfx(clickSound);
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception)
            {
                // no browser
            }
        }

        // ------------------------------------------------------------------------------------------
        // Window
        // ------------------------------------------------------------------------------------------

        // Moved onto a menu button (all buttons but the window buttons).
        private void MenuButton_MouseEnter(object sender, MouseEventArgs e)
        {
            PlaySfx(hoverSound);
        }

        // Hovering a mode lights up its side of the art: its glow layer fades in.
        private void ModeButton_MouseChanged(object sender, MouseEventArgs e)
        {
            UpdateGlow();
        }

        private void UpdateGlow()
        {
            Fade(SingleplayerGlow, !CardOpen && SingleplayerButton.IsMouseOver);
            Fade(MultiplayerGlow, !CardOpen && MultiplayerButton.IsMouseOver);
        }

        private static void Fade(UIElement layer, bool show)
        {
            layer.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 1.0 : 0.0, FadeTime));
        }

        private void Music_Click(object sender, RoutedEventArgs e)
        {
            PlaySfx(clickSound);
            settings.Music = !settings.Music;
            settings.Save();
            ShowMusicButton();
            if (settings.Music)
            {
                StartMusic();
            }
            else
            {
                StopMusic();
            }
        }

        private void Minimise_Click(object sender, RoutedEventArgs e)
        {
            PlaySfx(clickSound);
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            PlaySfx(backSound, true);
            Close();
        }

        // drag the window by anything that is not a button
        private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }
            e.Handled = true;
            if (OptionsOpen)
            {
                PlaySfx(backSound);
                SetOptionsOpen(false);
            }
            else if (MultiplayerOpen)
            {
                PlaySfx(backSound);
                SetMultiplayerOpen(false);
            }
            else
            {
                PlaySfx(backSound, true);
                Close();
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            StopMusic();
            try
            {
                if (musicFile != null)
                {
                    File.Delete(musicFile);
                }
            }
            catch (Exception)
            {
                // still in use (a second launcher): it stays in %TEMP%
            }
        }
    }
}