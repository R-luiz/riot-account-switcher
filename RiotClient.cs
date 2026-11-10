using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RiotAccountSwitcher
{
    /// <summary>What the running Riot Client reports about its sign-in.</summary>
    public sealed class LiveState
    {
        /// <summary>The local API answered.</summary>
        public bool Reachable;
        /// <summary>rso-auth session type: authenticated, needs_credentials, needs_multifactor_verification...</summary>
        public string SessionType;
        public bool PersistLogin;
        public string Puuid;
        public string GameName;
        public string TagLine;
        public string Platform;

        public bool Authenticated
        {
            get { return SessionType == "authenticated" && Puuid != null; }
        }
    }

    /// <summary>Finds, queries, stops and starts the Riot Client and the League client.</summary>
    public static class RiotClient
    {
        static readonly string LocalRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Riot Games", "Riot Client");

        public static readonly string DataDir = Path.Combine(LocalRoot, "Data");

        static readonly string LockFile = Path.Combine(LocalRoot, "Config", "lockfile");

        static readonly string InstallsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games", "RiotClientInstalls.json");

        // Everything that keeps the signed-in session in memory or the Data folder open.
        static readonly string[] ClientProcesses =
        {
            "LeagueClientUxRender", "LeagueClientUx", "LeagueClient", "LeagueCrashHandler64",
            "Riot Client", "RiotClientUxRender", "RiotClientUx", "RiotClientServices", "RiotClientCrashHandler",
        };

        // A match in progress. Never closed by the switcher.
        static readonly string[] GameProcesses = { "League of Legends", "VALORANT-Win64-Shipping" };

        // Closing the League client in these phases costs the player a dodge or a leave.
        static readonly string[] CommittedPhases =
        {
            "ReadyCheck", "ChampSelect", "GameStart", "InProgress", "Reconnect", "CheckedIntoTournament",
        };

        static readonly HttpClient Http = CreateHttp();

        static HttpClient CreateHttp()
        {
            // Both clients serve their local API over HTTPS with a self-signed certificate.
            // Requests carry the lockfile password, so they are only ever sent to this machine.
            var handler = new HttpClientHandler { UseProxy = false };
            handler.ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) => request.RequestUri.IsLoopback;
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(4) };
        }

        public static bool IsRunning()
        {
            return CountProcesses(ClientProcesses) > 0;
        }

        /// <summary>A sentence explaining why the clients must not be closed right now, or null.</summary>
        public static async Task<string> BlockReasonAsync()
        {
            if (CountProcesses(GameProcesses) > 0)
                return "A game is running. Finish it before switching accounts.";

            foreach (string leagueDir in LeagueDirs())
            {
                string phase = await GetAsync(Path.Combine(leagueDir, "lockfile"), "LeagueClient", "/lol-gameflow/v1/gameflow-phase") as string;
                if (phase != null && Array.IndexOf(CommittedPhases, phase) >= 0)
                    return "League is in " + phase + ". Closing it now could count as a dodge or a leave, so finish or leave that first.";
            }
            return null;
        }

        public static async Task<LiveState> QueryAsync()
        {
            var state = new LiveState();
            var session = await GetAsync(LockFile, "RiotClientServices", "/rso-auth/v1/session") as Dictionary<string, object>;
            if (session == null) return state;

            state.Reachable = true;
            state.SessionType = Text(session, "type");
            state.PersistLogin = session.TryGetValue("persistLogin", out object persist) && persist is bool && (bool)persist;
            if (state.SessionType != "authenticated") return state;

            // Neither of these two responses contains a token.
            var authorization = await GetAsync(LockFile, "RiotClientServices", "/rso-auth/v1/authorization") as Dictionary<string, object>;
            if (authorization != null)
            {
                state.Puuid = Text(authorization, "subject");
                state.Platform = Text(authorization, "currentPlatformId");
            }
            var userInfo = await GetAsync(LockFile, "RiotClientServices", "/riot-client-auth/v1/userinfo") as Dictionary<string, object>;
            if (userInfo != null && userInfo.TryGetValue("acct", out object acct) && acct is Dictionary<string, object> alias)
            {
                state.GameName = Text(alias, "game_name");
                state.TagLine = Text(alias, "tag_line");
            }
            return state;
        }

        /// <summary>Closes the League client and the Riot Client and returns once every process is gone.</summary>
        public static async Task StopAsync()
        {
            var clock = Stopwatch.StartNew();
            while (true)
            {
                var running = ClientProcesses.SelectMany(Process.GetProcessesByName).ToList();
                if (running.Count == 0) return;

                foreach (Process process in running)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (InvalidOperationException) { }   // exited on its own in the meantime
                    catch (Win32Exception) { }               // already terminating, or elevated: the timeout reports it
                    finally
                    {
                        process.Dispose();
                    }
                }

                if (clock.Elapsed > TimeSpan.FromSeconds(20))
                    throw new IOException("The Riot Client is still running and could not be closed. Close it from the system tray and try again.");
                await Task.Delay(200);
            }
        }

        /// <summary>Starts the Riot Client, which signs in with the stored session and opens League.</summary>
        public static void Launch()
        {
            string exe = ServicesExe();
            if (exe == null)
                throw new FileNotFoundException("RiotClientServices.exe was not found. Is the Riot Client installed?");

            var start = new ProcessStartInfo(exe, "--launch-product=league_of_legends --launch-patchline=live")
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exe),
            };
            using (Process.Start(start)) { }
        }

        static string ServicesExe()
        {
            var installs = ReadInstalls();
            foreach (string key in new[] { "rc_default", "rc_live" })
            {
                string exe = installs == null ? null : Text(installs, key);
                if (exe != null && File.Exists(exe)) return exe;
            }
            string fallback = @"C:\Riot Games\Riot Client\RiotClientServices.exe";
            return File.Exists(fallback) ? fallback : null;
        }

        static IEnumerable<string> LeagueDirs()
        {
            var installs = ReadInstalls();
            if (installs != null && installs.TryGetValue("associated_client", out object clients) && clients is Dictionary<string, object> map)
                return map.Keys.ToList();
            return new string[0];
        }

        static Dictionary<string, object> ReadInstalls()
        {
            try
            {
                return new JavaScriptSerializer().DeserializeObject(File.ReadAllText(InstallsFile)) as Dictionary<string, object>;
            }
            catch (IOException) { return null; }
            catch (ArgumentException) { return null; }           // not JSON
            catch (InvalidOperationException) { return null; }   // not JSON
        }

        /// <summary>
        /// GET on a client's local API, authenticated through its lockfile (name:pid:port:password:protocol).
        /// Null when that client is not running or does not answer with JSON.
        /// </summary>
        static async Task<object> GetAsync(string lockFile, string processName, string path)
        {
            string[] parts;
            try
            {
                using (var stream = new FileStream(lockFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream))
                    parts = reader.ReadToEnd().Split(':');
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            if (parts.Length < 5 || !int.TryParse(parts[1], out int pid) || !int.TryParse(parts[2], out int port)) return null;

            // A lockfile can outlive a crashed client; its port may belong to something else by now.
            try
            {
                using (Process owner = Process.GetProcessById(pid))
                    if (!owner.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)) return null;
            }
            catch (ArgumentException) { return null; }
            catch (InvalidOperationException) { return null; }

            using (var request = new HttpRequestMessage(HttpMethod.Get, "https://127.0.0.1:" + port + path))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("riot:" + parts[3])));
                try
                {
                    using (HttpResponseMessage response = await Http.SendAsync(request))
                    {
                        if (!response.IsSuccessStatusCode) return null;
                        return new JavaScriptSerializer().DeserializeObject(await response.Content.ReadAsStringAsync());
                    }
                }
                catch (HttpRequestException) { return null; }
                catch (TaskCanceledException) { return null; }       // timeout
                catch (ArgumentException) { return null; }           // not JSON
                catch (InvalidOperationException) { return null; }   // not JSON
            }
        }

        static string Text(Dictionary<string, object> map, string key)
        {
            return map.TryGetValue(key, out object value) ? value as string : null;
        }

        static int CountProcesses(string[] names)
        {
            int count = 0;
            foreach (string name in names)
                foreach (Process process in Process.GetProcessesByName(name))
                {
                    count++;
                    process.Dispose();
                }
            return count;
        }
    }
}
