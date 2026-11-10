using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace RiotAccountSwitcher
{
    /// <summary>What the Riot Client's Data folder currently holds.</summary>
    public sealed class SessionInspection
    {
        /// <summary>A "Stay signed in" session is stored.</summary>
        public bool HasSession;
        /// <summary>PUUID that session belongs to, when it can be read from the file.</summary>
        public string Puuid;
    }

    /// <summary>
    /// Reads and replaces the Riot Client's Data folder, where a "Stay signed in" session lives
    /// (RiotGamesPrivateSettings.yaml). The folder is copied as a whole so the switcher keeps working
    /// if Riot splits the session over more files. Every writer requires the Riot Client to be closed.
    /// </summary>
    public static class SessionStore
    {
        const string SettingsFile = "RiotGamesPrivateSettings.yaml";

        // Trusted-device cookie. It identifies this PC, not an account, and Riot keeps it across its own sign-out.
        const string DeviceSection = "rso-authenticator";

        // Session cookies and tokens are long opaque strings; flags, regions and nulls are not.
        const int MinSecretLength = 40;

        const string PackMagic = "RASP1";

        static readonly Regex UuidRegex = new Regex(
            "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.IgnoreCase);
        static readonly Regex SubjectClaimRegex = new Regex(
            "\"sub\"\\s*:\\s*\"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\"", RegexOptions.IgnoreCase);

        public static SessionInspection Inspect(string dataDir)
        {
            var result = new SessionInspection();
            string path = Path.Combine(dataDir, SettingsFile);
            if (!File.Exists(path)) return result;

            string tokenSubject = null;
            var plainIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string section = null;
            foreach (string line in ReadShared(path).Split('\n'))
            {
                if (IsSectionStart(line)) section = SectionName(line);
                if (section == DeviceSection) continue;

                string value = ScalarValue(line);
                if (value.Length == 0 || value == "null" || value == "~") continue;
                if (value.Length >= MinSecretLength) result.HasSession = true;
                if (UuidRegex.IsMatch(value)) plainIds.Add(value);
                else tokenSubject = tokenSubject ?? JwtSubject(value);
            }

            if (result.HasSession)
            {
                if (tokenSubject != null) result.Puuid = tokenSubject.ToLowerInvariant();
                else if (plainIds.Count == 1) foreach (string id in plainIds) result.Puuid = id.ToLowerInvariant();
            }
            return result;
        }

        /// <summary>Packs every file of the Data folder into one buffer.</summary>
        public static byte[] Capture(string dataDir)
        {
            string root = Path.GetFullPath(dataDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string[] files = Directory.Exists(root)
                ? Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                : new string[0];

            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer, Encoding.UTF8))
            {
                writer.Write(PackMagic);
                writer.Write(files.Length);
                foreach (string file in files)
                {
                    byte[] content = File.ReadAllBytes(file);
                    writer.Write(file.Substring(root.Length));
                    writer.Write(content.Length);
                    writer.Write(content);
                }
                writer.Flush();
                return buffer.ToArray();
            }
        }

        /// <summary>Replaces the Data folder with a pack made by <see cref="Capture"/>.</summary>
        public static void Restore(string dataDir, byte[] pack)
        {
            string root = Path.GetFullPath(dataDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var files = new List<KeyValuePair<string, byte[]>>();
            using (var reader = new BinaryReader(new MemoryStream(pack), Encoding.UTF8))
            {
                if (reader.ReadString() != PackMagic) throw new InvalidDataException("Saved sign-in has an unknown format.");
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string target = Path.GetFullPath(Path.Combine(root, reader.ReadString()));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Saved sign-in points outside the Riot Client data folder.");
                    files.Add(new KeyValuePair<string, byte[]>(target, reader.ReadBytes(reader.ReadInt32())));
                }
            }

            // Parsed completely before anything is deleted: a damaged pack must not cost the live session.
            Retry(() =>
            {
                Clear(root);
                foreach (var file in files)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file.Key));
                    File.WriteAllBytes(file.Key, file.Value);
                }
            });
        }

        /// <summary>
        /// Leaves the Data folder as Riot's own "Sign out" does: no session, trusted-device cookie kept,
        /// so the next sign-in is not treated as coming from a new PC.
        /// </summary>
        public static void SignOut(string dataDir)
        {
            string path = Path.Combine(dataDir, SettingsFile);
            string device = File.Exists(path) ? DeviceBlock(ReadShared(path)) : null;
            Retry(() =>
            {
                Clear(dataDir);
                if (device != null)
                {
                    Directory.CreateDirectory(dataDir);
                    File.WriteAllText(path, device, new UTF8Encoding(false));
                }
            });
        }

        static string DeviceBlock(string yaml)
        {
            var block = new StringBuilder();
            bool inside = false;
            foreach (string line in yaml.Split('\n'))
            {
                if (IsSectionStart(line)) inside = SectionName(line) == DeviceSection;
                if (inside && line.Trim().Length > 0) block.Append(line.TrimEnd('\r')).Append('\n');
            }
            return block.Length > 0 ? block.ToString() : null;
        }

        static bool IsSectionStart(string line)
        {
            return line.Length > 0 && !char.IsWhiteSpace(line[0]) && line[0] != '#' && line[0] != '-';
        }

        static string SectionName(string line)
        {
            int colon = line.IndexOf(':');
            return (colon < 0 ? line : line.Substring(0, colon)).Trim().Trim('"', '\'');
        }

        /// <summary>The scalar on a "key: value" or "- value" line; empty when the line only opens a nested block.</summary>
        static string ScalarValue(string line)
        {
            string text = line.Trim();
            while (text.StartsWith("- ", StringComparison.Ordinal)) text = text.Substring(2).TrimStart();
            int separator = text.IndexOf(": ", StringComparison.Ordinal);
            if (separator >= 0) text = text.Substring(separator + 2);
            else if (text.EndsWith(":", StringComparison.Ordinal)) return "";
            return text.Trim().Trim('"', '\'');
        }

        /// <summary>The "sub" claim of a JWT-shaped value, or null.</summary>
        static string JwtSubject(string value)
        {
            string[] parts = value.Split('.');
            if (parts.Length < 2 || !parts[0].StartsWith("eyJ", StringComparison.Ordinal)) return null;
            try
            {
                string payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                Match match = SubjectClaimRegex.Match(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
                return match.Success ? match.Groups[1].Value : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        static void Clear(string dir)
        {
            if (!Directory.Exists(dir)) return;
            foreach (string file in Directory.GetFiles(dir)) File.Delete(file);
            foreach (string sub in Directory.GetDirectories(dir)) Directory.Delete(sub, true);
        }

        static string ReadShared(string path)
        {
            // The Riot Client rewrites this file while it runs.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        /// <summary>File handles of a process that just exited can linger for a moment.</summary>
        static void Retry(Action action)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    action();
                    return;
                }
                catch (IOException) when (attempt < 5)
                {
                    Thread.Sleep(200);
                }
                catch (UnauthorizedAccessException) when (attempt < 5)
                {
                    Thread.Sleep(200);
                }
            }
        }
    }
}
