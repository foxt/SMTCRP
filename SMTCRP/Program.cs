using DiscordRPC;
using DiscordRPC.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.Media.Control;

namespace SMTCRP
{
    class Program
    {
        public static string[] icons = new string[] { "dopamine","winamp","zunemusic" };
        public static DiscordRpcClient client;
        static void Main(string[] args)
        {
            client = new DiscordRpcClient("404277856525352974")
            {
                Logger = new ConsoleLogger() { Level = LogLevel.Warning }
            };
            client.OnReady += (sender, e) =>
            {
                Console.WriteLine("Received Ready from user {0}", e.User.Username);
            };

            client.OnPresenceUpdate += (sender, e) =>
            {
                Console.WriteLine("Received Update! {0}", e.Presence);
            };

            client.Initialize();
            GlobalSystemMediaTransportControlsSessionManager gsmtcsm = GlobalSystemMediaTransportControlsSessionManager.RequestAsync().GetAwaiter().GetResult();
            bool isDisplaying = false;
            string lastData = "";
            while (true)
            {
                GC.Collect();
                Thread.Sleep(1000);
                try
                {
                    Console.WriteLine("Updating...");
                    var session = gsmtcsm.GetCurrentSession();
                    var ses = new List<GlobalSystemMediaTransportControlsSession>(gsmtcsm.GetSessions());
                    var i = 0;
                    while (session.SourceAppUserModelId.ToLower().StartsWith("discord") || session.SourceAppUserModelId.ToLower().StartsWith("firefox") || session.SourceAppUserModelId.ToLower().StartsWith("chrom") || session.SourceAppUserModelId.ToLower().StartsWith("opera"))
                    {
                        Console.WriteLine("Ignoring shitty browser media key hijacker {0}", session.SourceAppUserModelId);
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
                        Console.WriteLine("No media app open.");
                        continue;
                    }
                    string appName = session.SourceAppUserModelId.Replace(".exe", "");
                    var pbi = session.GetPlaybackInfo();
                    Console.WriteLine("{0} {1}", pbi.PlaybackStatus, isDisplaying);
                    if (pbi.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing && !isDisplaying)
                    {
                        continue;
                    }
                    if (pbi.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    {
                        client.ClearPresence();
                        isDisplaying = false;
                        Thread.Sleep(14000);
                        continue;
                    }
                    var tlp = session.GetTimelineProperties();
                    var mediaProperties = session.TryGetMediaPropertiesAsync().GetAwaiter().GetResult();
                    var data = mediaProperties.Title + " " + mediaProperties.Artist;
                    if (data == lastData && isDisplaying) { continue; }

                    Console.WriteLine("{0} - {1} via {2}", mediaProperties.Artist, mediaProperties.Title, appName);
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
                    Console.WriteLine(appLogo);
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
                    Thread.Sleep(14000);
                } catch(Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}
