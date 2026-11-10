using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace RiotAccountSwitcher
{
    public sealed class Account
    {
        /// <summary>PUUID.</summary>
        public string Id { get; set; }
        public string GameName { get; set; }
        public string TagLine { get; set; }
        /// <summary>League platform, e.g. EUW1.</summary>
        public string Platform { get; set; }
        /// <summary>The user's own note ("Main", "Smurf").</summary>
        public string Label { get; set; }
        /// <summary>Sealed sign-in (base64); null until the account has been switched away from once.</summary>
        public string Session { get; set; }
        public long AddedUtc { get; set; }

        [ScriptIgnore]
        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(GameName)) return "Account " + (Id ?? "").Substring(0, Math.Min(8, (Id ?? "").Length));
                return string.IsNullOrEmpty(TagLine) ? GameName : GameName + "#" + TagLine;
            }
        }
    }

    public sealed class VaultData
    {
        /// <summary>Random bytes (base64) that Windows Hello signs to produce the vault key. Not secret.</summary>
        public string Challenge { get; set; }
        /// <summary>Account whose session sits in the Riot Client's Data folder right now.</summary>
        public string LiveId { get; set; }
        public List<Account> Accounts { get; set; }

        public Account Find(string id)
        {
            if (id == null) return null;
            return Accounts.Find(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// The account list plus each inactive account's sealed sign-in. A sign-in is sealed with DPAPI
    /// (this Windows user, this PC) using a key that only a Windows Hello gesture can reproduce as extra
    /// entropy, so neither another user nor a process without the PIN can open it.
    /// </summary>
    public sealed class Vault
    {
        readonly string path;
        public VaultData Data { get; private set; }

        Vault(string path, VaultData data)
        {
            this.path = path;
            Data = data;
        }

        public static Vault Load(string path)
        {
            VaultData data = null;
            if (File.Exists(path))
                data = new JavaScriptSerializer().Deserialize<VaultData>(File.ReadAllText(path, Encoding.UTF8));
            data = data ?? new VaultData();
            data.Accounts = data.Accounts ?? new List<Account>();
            if (data.Challenge == null)
            {
                byte[] challenge = new byte[32];
                using (var random = RandomNumberGenerator.Create()) random.GetBytes(challenge);
                data.Challenge = Convert.ToBase64String(challenge);
            }
            return new Vault(path, data);
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, new JavaScriptSerializer().Serialize(Data), new UTF8Encoding(false));
            // Replace in one step: a crash mid-write must not leave a truncated vault.
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        public static string Seal(byte[] session, byte[] key)
        {
            return Convert.ToBase64String(ProtectedData.Protect(session, key, DataProtectionScope.CurrentUser));
        }

        /// <exception cref="CryptographicException">Wrong key, another Windows user, or a damaged blob.</exception>
        public static byte[] Open(string sealedSession, byte[] key)
        {
            return ProtectedData.Unprotect(Convert.FromBase64String(sealedSession), key, DataProtectionScope.CurrentUser);
        }
    }
}
