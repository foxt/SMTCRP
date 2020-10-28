using DiscordRPC;
using DiscordRPC.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.Media.Control;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Timers;

namespace SMTCRP
{
    class Program
    {
        #region Console Allocation
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        public static void ShowConsoleWindow()
        {
            var handle = GetConsoleWindow();

            if (handle == IntPtr.Zero)
            {
                AllocConsole();
            }
            else
            {
                ShowWindow(handle, SW_SHOW);
            }
        }

        public static void HideConsoleWindow()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
        }
        #endregion
        public static string[] icons = new string[] { "dopamine","winamp","zunemusic" };
        public static DiscordRpcClient client;
        public static NotifyIcon trayIcon = new NotifyIcon();
        public static GlobalSystemMediaTransportControlsSessionManager gsmtcsm = GlobalSystemMediaTransportControlsSessionManager.RequestAsync().GetAwaiter().GetResult();
        public static bool isDisplaying = false;
        public static string lastData = "";
        public static System.Timers.Timer updTimer = new System.Timers.Timer(1000);
        public static System.Timers.Timer deferTimer = new System.Timers.Timer(14000);
        public static Icon tray = new Icon("Resources/tray.ico");
        public static Icon trey = new Icon("Resources/trey.ico");
        static void Main(string[] args)
        {
            #region UI
            AllocConsole();
            Console.Title = "SMTCRP Log - Close this window to quit SMTCRP";
            HideConsoleWindow();
            bool consoleShowing = false;
            trayIcon.Text = "SMTCRP";
            trayIcon.Icon = tray;
            trayIcon.DoubleClick += (object sender, EventArgs evt) =>
            {
                Console.WriteLine("User clicked tray icon");
                if (consoleShowing)
                {
                    HideConsoleWindow();
                    consoleShowing = false;
                }
                else
                {
                    ShowConsoleWindow();
                    consoleShowing = true;
                }
            };

            trayIcon.Visible = true;
            #endregion

            #region Discord RPC Init
            client = new DiscordRpcClient("404277856525352974")
            {
                Logger = new ConsoleLogger() { Level = LogLevel.Warning }
            };
            client.OnReady += (sender, e) =>
            {
                string[] greetings = new string[] { "Howdy", "Hi","Hey","Welcome", "Bonjour","Hola","Ni hao", "Ciao", "Konnichiwa","Guten Tag","Ola","Hej"};
                Console.WriteLine("{1}, {0}!", e.User.Username, greetings[new Random().Next(0, greetings.Length)]);
                if (!updTimer.Enabled && !deferTimer.Enabled)
                {
                    updTimer.Start();
                }
            };

            client.OnPresenceUpdate += (sender, e) =>
            {
                Console.WriteLine("Recieved confirmation that presence is {0}", e.Presence.Details);
            };

            client.Initialize();
            #endregion

            updTimer.Elapsed += update;
            updTimer.AutoReset = true;
            updTimer.Enabled = false;
            deferTimer.Elapsed += RestartUpdateTimer;
            deferTimer.AutoReset = false;
            deferTimer.Enabled = false;
            Application.Run();
            
        }

        private static void RestartUpdateTimer(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine("Restarting update cycle...");
            deferTimer.Stop();
            updTimer.Start();
        }

        public static void update(object sender, ElapsedEventArgs arg)
        {
            GC.Collect();
            try
            {
                //Console.WriteLine("Updating...");
                var session = gsmtcsm.GetCurrentSession();
                var ses = new List<GlobalSystemMediaTransportControlsSession>(gsmtcsm.GetSessions());
                var i = 0;
                //TODO: make this nicer
                while (session.SourceAppUserModelId.ToLower().StartsWith("discord") ||
                    session.SourceAppUserModelId.ToLower().StartsWith("firefox") ||
                    session.SourceAppUserModelId.ToLower().StartsWith("chrom") ||
                    session.SourceAppUserModelId.ToLower().StartsWith("opera") ||
                    session.SourceAppUserModelId.ToLower().StartsWith("spotify"))
                {
                    Console.WriteLine("Ignoring {0}", session.SourceAppUserModelId);
                    if (session == null || i >= ses.Count)
                    {
                        session = null;
                        break;
                    }
                    session = ses[i];
                    i += 1;
                }
                if (session == null)
                {
                    trayIcon.Icon = trey;
                    trayIcon.Text = "SMTCRP - No media session";
                    if (isDisplaying)
                    {
                        isDisplaying = false;
                        client.ClearPresence();
                        Console.WriteLine("No media app open, Defering update cycle for 14 seconds...");
                        updTimer.Stop();
                        deferTimer.Start();

                    }
                    return;
                }
                string appName = session.SourceAppUserModelId.Replace(".exe", "");
                var pbi = session.GetPlaybackInfo();
                if (pbi.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing && !isDisplaying)
                {
                    return;
                }
                if (pbi.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    trayIcon.Icon = trey;
                    trayIcon.Text = "SMTCRP - Nothing playing";
                    client.ClearPresence();
                    isDisplaying = false;
                    Console.WriteLine("Playback is {0}, Defering update cycle for 14 seconds...", pbi.PlaybackStatus);
                    updTimer.Stop();
                    deferTimer.Start();
                    return;
                }
                var tlp = session.GetTimelineProperties();
                var mediaProperties = session.TryGetMediaPropertiesAsync().GetAwaiter().GetResult();


                string appLogo = "generic";
                if (icons.Contains(appName.ToLower()))
                {
                    appLogo = appName.ToLower();
                }
                if (appName.ToLower().StartsWith("winamp") || appName.ToLower().StartsWith("wacup"))
                {
                    appLogo = "winamp";
                    appName = "Winamp";
                }
                if (appName.EndsWith("ZuneMusic"))
                {
                    appLogo = "zunemusic";
                    appName = "Groove Music";
                }
                if (appLogo == "generic")
                {
                    Console.WriteLine("Using generic app icon! Contact theLMGN#4444 to get an app icon for {0}", appName);
                }
                if (appName.Contains("!")) // attempt to clean UWP app names
                {
                    appName = appName.Split(".").Last();
                }
                var data = mediaProperties.Title + " by " + mediaProperties.Artist + " via " + appName;
                if (data == lastData && isDisplaying) { return; }
                Console.WriteLine(data);
                client.SetPresence(new RichPresence()
                {
                    Details = "🎵" + mediaProperties.Title,
                    State = "👤" + mediaProperties.Artist,
                    Assets = new Assets()
                    {
                        LargeImageKey = appLogo,
                        LargeImageText = "via " + appName,
                        SmallImageKey = "logo",
                        SmallImageText = "https://github.com/thelmgn/smtcrp"
                    }
                });
                isDisplaying = true;
                lastData = data;
                trayIcon.Icon = tray;
                if (data.Length > 54)
                {
                    trayIcon.Text = "SMTCRP - " + data.Substring(0, 53) + "…";
                } else
                {
                    trayIcon.Text = "SMTCRP - " + data;
                }
                Console.WriteLine("Defering update cycle for 14 seconds...");
                updTimer.Stop();
                deferTimer.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
