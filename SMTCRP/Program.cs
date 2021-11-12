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
        public static string[] icons = new string[] { "dopamine", "winamp", "zunemusic", "tidal" };
        public static DiscordRpcClient client;
        public static NotifyIcon trayIcon = new NotifyIcon();
        public static ContextMenuStrip context = new ContextMenuStrip();
        public static GlobalSystemMediaTransportControlsSessionManager gsmtcsm = GlobalSystemMediaTransportControlsSessionManager.RequestAsync().GetAwaiter().GetResult();
        public static bool isDisplaying = false;
        public static bool settingsUpdated = false;
        public static string lastData = "";
        public static System.Timers.Timer updTimer = new System.Timers.Timer(1000);
        public static System.Timers.Timer deferTimer = new System.Timers.Timer(14000);
        public static Icon tray = new Icon("Resources/tray.ico");
        public static Icon trey = new Icon("Resources/trey.ico");
        public static Icon troy = new Icon("Resources/troy.ico");

        public static ToolStripMenuItem showalbumtitle = new ToolStripMenuItem("Show album title");
        public static ToolStripMenuItem showtracknumber = new ToolStripMenuItem("Show track number");
        public static ToolStripMenuItem trayonstart = new ToolStripMenuItem("Tray on startup");
        public static ToolStripMenuItem blockbrowsers = new ToolStripMenuItem("Auto block browsers");
        public static ToolStripMenuItem blockapp = new ToolStripMenuItem("Block current app");

        public static string currentSession = null;

        static void Main(string[] args)
        {
            #region UI
            AllocConsole();
            Console.Title = "SMTCRP Log - Closing this window quits SMTCRP";
            bool consoleShowing = !Properties.Settings.Default.trayOnStartup;
            trayIcon.Text = "SMTCRP - Waiting";
            trayIcon.Icon = trey;

            if (!consoleShowing)
            {
                HideConsoleWindow();
            }

            context.Items.Add("Toggle Console", null, (object sender, EventArgs evt) =>
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
            });

            showalbumtitle.Checked = Properties.Settings.Default.useAlbumTitle;
            showalbumtitle.Click += (object sender, EventArgs evt) =>
            {
                Properties.Settings.Default.useAlbumTitle = !Properties.Settings.Default.useAlbumTitle;
                Properties.Settings.Default.Save();
                showalbumtitle.Checked = Properties.Settings.Default.useAlbumTitle;
            };
            showtracknumber.Checked = Properties.Settings.Default.useTrackNumbers;
            showtracknumber.Click += (object sender, EventArgs evt) =>
            {
                Properties.Settings.Default.useTrackNumbers = !Properties.Settings.Default.useTrackNumbers;
                Properties.Settings.Default.Save();
                showtracknumber.Checked = Properties.Settings.Default.useTrackNumbers;
            };
            trayonstart.Checked = Properties.Settings.Default.trayOnStartup;
            trayonstart.Click += (object sender, EventArgs evt) =>
            {
                Properties.Settings.Default.trayOnStartup = !Properties.Settings.Default.trayOnStartup;
                Properties.Settings.Default.Save();
                trayonstart.Checked = Properties.Settings.Default.trayOnStartup;
            };
            blockbrowsers.Checked = Properties.Settings.Default.blockBrowsers;
            blockbrowsers.Click += (object sender, EventArgs evt) =>
            {
                Properties.Settings.Default.blockBrowsers = !Properties.Settings.Default.blockBrowsers;
                Properties.Settings.Default.Save();
                blockbrowsers.Checked = Properties.Settings.Default.blockBrowsers;
            };
            blockapp.Enabled = false;
            blockapp.Click += (object sender, EventArgs evt) =>
            {
                if (Properties.Settings.Default.blockedApps.Contains(currentSession))
                {
                    Properties.Settings.Default.blockedApps.Remove(currentSession);
                }
                else
                {
                    Properties.Settings.Default.blockedApps.Add(currentSession);
                }
                Properties.Settings.Default.Save();
                blockapp.Checked = Properties.Settings.Default.blockedApps.Contains(currentSession);
            };


            Properties.Settings.Default.SettingsSaving += (object sender, System.ComponentModel.CancelEventArgs evt) =>
            {
                Console.WriteLine("Settings updated, Presence will update as soon as it's allowed");
                isDisplaying = false;
                settingsUpdated = true;
            };

            context.Items.Add(showalbumtitle);
            context.Items.Add(showtracknumber);
            context.Items.Add(blockbrowsers);
            context.Items.Add(blockapp);
            context.Items.Add(trayonstart);

            context.Items.Add("Exit SMTCRP", null, (object sender, EventArgs evt) =>
            {
                client.Dispose();
                Application.Exit();
            });

            trayIcon.ContextMenuStrip = context;

            trayIcon.Visible = true;
            #endregion

            #region Discord RPC Init
            client = new DiscordRpcClient("404277856525352974")
            {
                Logger = new ConsoleLogger() { Level = LogLevel.Warning }
            };
            client.OnReady += (sender, e) =>
            {
                string[] greetings = new string[] { "Howdy", "Hi", "Hey", "Welcome", "Bonjour", "Hola", "Ni hao", "Ciao", "Konnichiwa", "Guten Tag", "Ola", "Hej", "Yo", "What's up", "Kamusta" };
                Console.WriteLine("{1}, {0}!", e.User.Username, greetings[new Random().Next(0, greetings.Length)]);
                if (!updTimer.Enabled && !deferTimer.Enabled)
                {
                    updTimer.Start();
                }
            };

            client.OnPresenceUpdate += (sender, e) =>
            {
                try
                {
                    Console.WriteLine("Recieved confirmation that presence is {0}", e.Presence.Details);
                }
                catch (Exception)
                {
                    Console.WriteLine("Presence cleared!");
                }
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

                if (session != null)
                {
                    currentSession = session.SourceAppUserModelId.ToLower();

                    if (blockapp.GetCurrentParent().InvokeRequired)
                    {
                        blockapp.GetCurrentParent().Invoke(new Action(() =>
                        {
                        // Do anything you want with the control here
                        blockapp.Checked = Properties.Settings.Default.blockedApps.Contains(currentSession);
                            blockapp.Enabled = true;
                        }));
                    }
                    else
                    {
                        blockapp.Checked = Properties.Settings.Default.blockedApps.Contains(currentSession);
                        blockapp.Enabled = true;
                    }
                }
                else
                {
                    currentSession = null;
                    if (blockapp.GetCurrentParent().InvokeRequired)
                    {
                        blockapp.GetCurrentParent().Invoke(new Action(() =>
                        {
                            blockapp.Text = "No media session";
                            blockapp.Checked = false;
                            blockapp.Enabled = false;
                        }));
                    }
                    else
                    {
                        blockapp.Text = "No media session";
                        blockapp.Checked = false;
                        blockapp.Enabled = false;
                    }
                }

                //var browser_blocked = false;

                if (session == null)
                {
                    trayIcon.Icon = trey;
                    trayIcon.Text = "SMTCRP - No media session";
                    if (isDisplaying)
                    {
                        isDisplaying = false;
                        client.ClearPresence();
                        Console.WriteLine("No media app open, Defering update cycle for 14 seconds...");
                        settingsUpdated = false;
                        updTimer.Stop();
                        deferTimer.Start();

                    }
                    return;
                }
                else
                {
                    //TODO: make this nicer
                    if (Properties.Settings.Default.blockBrowsers)
                    {
                        while (session.SourceAppUserModelId.ToLower().StartsWith("discord") ||
                            session.SourceAppUserModelId.ToLower().StartsWith("firefox") ||
                            session.SourceAppUserModelId.ToLower().StartsWith("chrom") ||
                            session.SourceAppUserModelId.ToLower().StartsWith("opera") ||
                            session.SourceAppUserModelId.ToLower().StartsWith("msedge") ||
                            session.SourceAppUserModelId.ToLower().StartsWith("spotify"))
                        {
                            //Console.WriteLine("browser_blocker {0}", session.SourceAppUserModelId);
                            if (session == null || i >= ses.Count)
                            {
                                session = null;
                                break;
                            }
                            session = ses[i];
                            //browser_blocked = true;
                            i += 1;
                        }
                    }
                }

                if (session != null)
                {
                    var mediaProperties = session.TryGetMediaPropertiesAsync().GetAwaiter().GetResult();
                    string appName = session.SourceAppUserModelId.Replace(".exe", "");
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
                    if (appName.ToLower().StartsWith("tidal"))
                    {
                        appLogo = "tidal";
                        appName = "TIDAL";
                    }
                    if (appName.EndsWith("ZuneMusic"))
                    {
                        appLogo = "zunemusic";
                        appName = "Groove Music";
                    }
                    if (appName.Contains("!")) // attempt to clean UWP app names
                    {
                        appName = appName.Split(".").Last();
                    }
                    var data = mediaProperties.Title + " by " + mediaProperties.Artist + " via " + appName;
                    if (blockapp.GetCurrentParent().InvokeRequired)
                    {
                        blockapp.GetCurrentParent().Invoke(new Action(() =>
                        {
                            blockapp.Text = "Block " + appName;
                        }));
                    }
                    else
                    {
                        blockapp.Text = "Block " + appName;
                    }

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
                        settingsUpdated = false;
                        updTimer.Stop();
                        deferTimer.Start();
                        return;
                    }
                    var tlp = session.GetTimelineProperties();

                    if (data == lastData && isDisplaying) { return; }
                    Console.WriteLine(data);

                    var count = mediaProperties.AlbumTrackCount;
                    var num = mediaProperties.TrackNumber;

                    if (count < num)
                    {
                        count = num;
                    }

                    if (Properties.Settings.Default.blockedApps.Contains(session.SourceAppUserModelId.ToLower()))
                    {
                        //Console.WriteLine("user_blocker {0}", session.SourceAppUserModelId);
                        trayIcon.Icon = troy;
                        trayIcon.Text = "SMTCRP - " + appName + " is blocked";
                        client.ClearPresence();
                        isDisplaying = false;
                        //Console.WriteLine("Playback is {0}, Defering update cycle for 14 seconds...", pbi.PlaybackStatus);
                        //updTimer.Stop();
                        //deferTimer.Start();
                    }
                    else
                    {
                        var generated_party = new Party()
                        {
                            ID = Guid.NewGuid().ToString(),
                            Size = num,
                            Max = count
                        };

                        if (!Properties.Settings.Default.useTrackNumbers)
                        {
                            generated_party = null;
                        }

                        var generated_details = "🎵 " + mediaProperties.Title;
                        if (Properties.Settings.Default.useAlbumTitle)
                        {
                            generated_details = "🎵 " + mediaProperties.AlbumTitle + " - " + mediaProperties.Title;
                        }

                        if (appLogo == "generic")
                        {
                            Console.WriteLine("Using generic app icon! Contact theLMGN#4444 to get an app icon for {0}", appName);
                        }

                        client.SetPresence(new RichPresence()
                        {
                            Details = generated_details,
                            State = "👥 " + mediaProperties.Artist,
                            Party = generated_party,
                            Assets = new Assets()
                            {
                                LargeImageKey = appLogo,
                                LargeImageText = "via " + appName,
                                SmallImageKey = "logo",
                                SmallImageText = "https://github.com/thelmgn/smtcrp"
                            },
                        });
                        isDisplaying = true;
                        lastData = data;
                        trayIcon.Icon = tray;
                        if (data.Length > 54)
                        {
                            trayIcon.Text = "SMTCRP - " + data.Substring(0, 53) + "…";
                        }
                        else
                        {
                            trayIcon.Text = "SMTCRP - " + data;
                        }
                    }
                }
                else
                {
                    trayIcon.Icon = trey;
                    trayIcon.Text = "SMTCRP - Ignoring a browser";
                    if (isDisplaying || settingsUpdated)
                    {
                        isDisplaying = false;
                        client.ClearPresence();
                        Console.WriteLine("Browser ignored, Defering update cycle for 14 seconds...");
                        settingsUpdated = false;
                        updTimer.Stop();
                        deferTimer.Start();
                    }
                    return;
                }
                Console.WriteLine("Defering update cycle for 14 seconds...");
                settingsUpdated = false;
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
