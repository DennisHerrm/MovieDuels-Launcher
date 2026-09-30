using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Resources;

namespace MovieDuelsLauncher
{
	// The launcher window (layout and looks: MainWindow.xaml).
	//
	// - SINGLEPLAYER MODE / MULTIPLAYER MODE start MovieDuels-SP.x86_64.exe / MovieDuels-MP.x86_64.exe
	//   from the launcher's folder, passing the OPTIONS as "+set" values, and close the launcher.
	// - The gear opens the OPTIONS page on the info card: Renderer, Display, Animations
	//   (remembered by LauncherSettings).
	// - The other icons and HELP open web pages in the browser (each button's Tag in the XAML).
	public partial class MainWindow : Window
	{
		const string SPExe = "MovieDuels-SP.x86_64.exe";
		const string MPExe = "MovieDuels-MP.x86_64.exe";

		const double MusicVolume = 0.6;
		static readonly TimeSpan FadeTime = TimeSpan.FromMilliseconds(180); // the glow layers

		readonly string folder; // where the launcher (and the game exes) are
		readonly LauncherSettings settings;

		// menu sounds (the in-game menu's own); a newer one cuts off the one still playing
		readonly SoundPlayer clickSound;
		readonly SoundPlayer hoverSound;
		readonly SoundPlayer backSound;

		// background music: MediaPlayer can only play a file, so the MP3 is written to %TEMP% first
		MediaPlayer music;
		string musicFile;

		public MainWindow()
		{
			folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			settings = new LauncherSettings(folder);

			InitializeComponent();

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

		static Stream OpenResource(string path)
		{
			StreamResourceInfo info = Application.GetResourceStream(new Uri(path, UriKind.Relative));
			return info?.Stream;
		}

		static SoundPlayer LoadSound(string path)
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
		static void PlaySfx(SoundPlayer sound, bool wait = false)
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

		void StartMusic()
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

		void StopMusic()
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

		void ShowOptions()
		{
			RendererButton.Content = settings.Rend2 ? "RENDERER: REND2" : "RENDERER: VANILLA";
			DisplayButton.Content = settings.Windowed ? "DISPLAY: WINDOWED" : "DISPLAY: FULLSCREEN";
			AnimationsButton.Content = settings.MasterAnimations ? "ANIMATIONS: MASTER" : "ANIMATIONS: CLASSIC";
		}

		void ShowMusicButton()
		{
			// speaker: waves when the music is on, a cross when it is off
			MusicOnMark.Visibility = settings.Music ? Visibility.Visible : Visibility.Collapsed;
			MusicOffMark.Visibility = settings.Music ? Visibility.Collapsed : Visibility.Visible;
		}

		bool OptionsOpen => OptionsPage.Visibility == Visibility.Visible;

		void SetOptionsOpen(bool open)
		{
			OptionsPage.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
			MainPage.IsHitTestVisible = !open; // the main page's buttons are covered by the options page
			UpdateGlow();
		}

		void Options_Click(object sender, RoutedEventArgs e)
		{
			PlaySfx(clickSound);
			SetOptionsOpen(true);
		}

		void OptionsBack_Click(object sender, RoutedEventArgs e)
		{
			PlaySfx(backSound);
			SetOptionsOpen(false);
		}

		void Renderer_Click(object sender, RoutedEventArgs e)
		{
			settings.Rend2 = !settings.Rend2;
			OptionChanged();
		}

		void Display_Click(object sender, RoutedEventArgs e)
		{
			settings.Windowed = !settings.Windowed;
			OptionChanged();
		}

		void Animations_Click(object sender, RoutedEventArgs e)
		{
			settings.MasterAnimations = !settings.MasterAnimations;
			OptionChanged();
		}

		void OptionChanged()
		{
			PlaySfx(clickSound);
			settings.Save();
			ShowOptions();
		}

		// ------------------------------------------------------------------------------------------
		// Starting the game, links
		// ------------------------------------------------------------------------------------------

		bool StartGame(bool multiplayer)
		{
			string exe = Path.Combine(folder, multiplayer ? MPExe : SPExe);
			if (!File.Exists(exe))
			{
				MessageBox.Show(this, "Could not find " + exe + "\n\nThe launcher must be in the MovieDuels game folder.",
					"MovieDuels", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}

			// "+set" on the command line overrides the values in the configs
			string renderer = multiplayer
				? (settings.Rend2 ? "MovieDuels-rdmp-rend2" : "MovieDuels-rdmp")
				: (settings.Rend2 ? "MovieDuels-rdsp-rend2" : "MovieDuels-rdsp");
			string args = "+set cl_renderer " + renderer;
			args += settings.Rend2 ? " +set com_rend2 1" : " +set com_rend2 0";
			args += settings.Windowed ? " +set r_fullscreen 0" : " +set r_fullscreen 1";
			if (!multiplayer)
			{
				args += settings.MasterAnimations ? " +set g_ActivateAnimationStyle 1" : " +set g_ActivateAnimationStyle 0";
			}

			try
			{
				Process.Start(new ProcessStartInfo(exe, args) { WorkingDirectory = folder, UseShellExecute = false })?.Dispose();
				return true;
			}
			catch (Exception)
			{
				MessageBox.Show(this, "Could not start " + exe, "MovieDuels", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}
		}

		void Play(bool multiplayer)
		{
			PlaySfx(clickSound, true); // the launcher closes right after
			if (StartGame(multiplayer))
			{
				Close();
			}
		}

		void Singleplayer_Click(object sender, RoutedEventArgs e)
		{
			Play(false);
		}

		void Multiplayer_Click(object sender, RoutedEventArgs e)
		{
			Play(true);
		}

		// A link button: the web address is the button's Tag.
		void Link_Click(object sender, RoutedEventArgs e)
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
		void MenuButton_MouseEnter(object sender, MouseEventArgs e)
		{
			PlaySfx(hoverSound);
		}

		// Hovering a mode lights up its side of the art: its glow layer fades in.
		void ModeButton_MouseChanged(object sender, MouseEventArgs e)
		{
			UpdateGlow();
		}

		void UpdateGlow()
		{
			Fade(SingleplayerGlow, !OptionsOpen && SingleplayerButton.IsMouseOver);
			Fade(MultiplayerGlow, !OptionsOpen && MultiplayerButton.IsMouseOver);
		}

		static void Fade(UIElement layer, bool show)
		{
			layer.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 1.0 : 0.0, FadeTime));
		}

		void Music_Click(object sender, RoutedEventArgs e)
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

		void Minimise_Click(object sender, RoutedEventArgs e)
		{
			PlaySfx(clickSound);
			WindowState = WindowState.Minimized;
		}

		void Close_Click(object sender, RoutedEventArgs e)
		{
			PlaySfx(backSound, true);
			Close();
		}

		// drag the window by anything that is not a button
		void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ButtonState == MouseButtonState.Pressed)
			{
				DragMove();
			}
		}

		void Window_PreviewKeyDown(object sender, KeyEventArgs e)
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
			else
			{
				PlaySfx(backSound, true);
				Close();
			}
		}

		void Window_Closed(object sender, EventArgs e)
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
