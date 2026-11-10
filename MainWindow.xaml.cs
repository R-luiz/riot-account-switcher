using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace RiotAccountSwitcher
{
    /// <summary>One card in the account list.</summary>
    public sealed class AccountRow
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public string Detail { get; set; }
        public string Chip { get; set; }
        public Brush ChipBrush { get; set; }
        public string Initial { get; set; }
        public Brush AvatarBrush { get; set; }
        public string Hint { get; set; }

        public bool HasDetail { get { return !string.IsNullOrEmpty(Detail); } }
        public bool HasChip { get { return Chip != null; } }
    }

    public partial class MainWindow : Window
    {
        /// <summary>Status line while nothing is happening. An empty list explains itself.</summary>
        string IdleStatus
        {
            get { return vault.Data.Accounts.Count == 0 ? "" : "Click an account to switch. Windows Hello unlocks its saved sign-in."; }
        }

        const string TickReminder = "Sign in in the Riot Client and tick “Stay signed in”.";

        static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(15);

        // A restored session takes the client a few seconds to resume; only a sign-in form
        // that stays up this many one-second polls means the session was refused.
        const int ExpiredAfterPolls = 12;

        static readonly Color[] AvatarColors =
        {
            Color.FromRgb(0xD1, 0x36, 0x39), Color.FromRgb(0x2F, 0x80, 0xED), Color.FromRgb(0x3F, 0xB6, 0x8B),
            Color.FromRgb(0xC7, 0x8B, 0x2E), Color.FromRgb(0x8E, 0x5B, 0xD9), Color.FromRgb(0x2A, 0xA5, 0xB8),
        };

        /// <summary>Result of an operation started from the window.</summary>
        sealed class Outcome
        {
            /// <summary>Shown in the status line when the operation ends.</summary>
            public string Status;
            /// <summary>The Riot Client was started and its sign-in should be watched.</summary>
            public bool Started;
            /// <summary>The account was signed in already; nothing was swapped.</summary>
            public bool AlreadyActive;
            /// <summary>Account the user asked for; null when adding a new one.</summary>
            public Account Target;
            /// <summary>A saved sign-in was put in place (as opposed to an empty sign-in screen).</summary>
            public bool Restored;
        }

        readonly Vault vault = Vault.Load(Path.Combine(Log.AppDir, "vault.json"));

        // An operation owns the window. Closing waits for it: leaving mid-swap would strand the Data
        // folder, and Windows keeps a Hello prompt on screen after the program that asked for it is gone.
        bool busy;
        CancellationTokenSource watcher;   // the wait for a sign-in after the Riot Client was started

        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += (sender, e) => App.UseDarkTitleBar(this);
            Loaded += async (sender, e) => await RunAsync("Checking the Riot Client…", async () =>
            {
                await SyncLiveAsync();
                return new Outcome();
            });
            Closing += (sender, e) =>
            {
                if (busy) e.Cancel = true;
                else if (watcher != null) watcher.Cancel();
            };
        }

        // ---- operations ------------------------------------------------------------------------

        async void OnAddClick(object sender, RoutedEventArgs e)
        {
            await SwitchAsync(null);
        }

        async void OnAccountClick(object sender, RoutedEventArgs e)
        {
            Account target = vault.Data.Find((string)((Button)sender).Tag);
            if (target != null) await SwitchAsync(target);
        }

        async Task SwitchAsync(Account target)
        {
            Outcome outcome = await RunAsync("Checking the Riot Client…", () => ActivateAsync(target));
            if (outcome == null || !outcome.Started) return;

            if (outcome.AlreadyActive)
            {
                await Task.Delay(1500);
                Close();
            }
            else
            {
                await WatchSignInAsync(outcome);
            }
        }

        /// <summary>
        /// Makes <paramref name="target"/> the account the Riot Client signs in with: saves the session that
        /// is in place now, puts the target's saved session there (or an empty sign-in screen when it has
        /// none, or when adding), and starts the client.
        /// </summary>
        async Task<Outcome> ActivateAsync(Account target)
        {
            string blocked = await RiotClient.BlockReasonAsync();
            if (blocked != null)
            {
                ShowBanner(blocked, true);
                return new Outcome();
            }

            LiveState live = await SyncLiveAsync();
            VaultData data = vault.Data;
            Account current = data.Find(data.LiveId);
            bool stored = SessionStore.Inspect(RiotClient.DataDir).HasSession || (live.Authenticated && live.PersistLogin);

            if (target != null && target == current && (live.Authenticated || stored))
            {
                RiotClient.Launch();
                Log.Write("Opened League for the signed-in account " + target.DisplayName);
                return new Outcome { Started = true, AlreadyActive = true, Status = target.DisplayName + " is already signed in. Opening League…" };
            }

            if (target == null && RiotClient.IsRunning() && MessageBox.Show(this,
                    "Adding an account closes League and the Riot Client, then opens the Riot sign-in screen.\n\nContinue?",
                    App.Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return new Outcome();

            bool saveCurrent = current != null && stored;
            byte[] key = null;
            byte[] session = null;
            try
            {
                if (saveCurrent || (target != null && target.Session != null))
                {
                    SetStatus("Confirm with Windows Hello…", true);
                    key = await HelloKey.DeriveAsync(Convert.FromBase64String(data.Challenge));
                    if (key == null) return new Outcome { Status = "Cancelled. Nothing was changed." };
                }

                SetStatus("Closing the Riot Client…", true);
                await RiotClient.StopAsync();

                // The live session is the only fresh copy of the current account's sign-in (the client
                // rotates its tokens while it runs), so it is saved on every switch away.
                if (saveCurrent)
                {
                    current.Session = Vault.Seal(SessionStore.Capture(RiotClient.DataDir), key);
                    vault.Save();
                    Log.Write("Saved the sign-in of " + current.DisplayName);
                }

                if (target != null && target.Session != null)
                {
                    try
                    {
                        session = Vault.Open(target.Session, key);
                    }
                    catch (CryptographicException)
                    {
                        // Sealed with a Windows Hello key that no longer exists (PIN reset), or damaged.
                        target.Session = null;
                        Log.Write("The saved sign-in of " + target.DisplayName + " could not be opened; asking for a new sign-in");
                    }
                }

                if (session != null)
                {
                    SetStatus("Restoring " + target.DisplayName + "…", true);
                    SessionStore.Restore(RiotClient.DataDir, session);
                    data.LiveId = target.Id;
                }
                else
                {
                    SessionStore.SignOut(RiotClient.DataDir);
                    data.LiveId = null;
                }
                vault.Save();

                RiotClient.Launch();
                Log.Write(session != null ? "Switched to " + target.DisplayName
                    : target != null ? "Opened the sign-in screen for " + target.DisplayName
                    : "Opened the sign-in screen for a new account");
                return new Outcome { Started = true, Target = target, Restored = session != null };
            }
            finally
            {
                if (key != null) Array.Clear(key, 0, key.Length);
                if (session != null) Array.Clear(session, 0, session.Length);
            }
        }

        /// <summary>Follows the Riot Client after a start until an account is signed in, then records which one.</summary>
        async Task WatchSignInAsync(Outcome launched)
        {
            var cancel = new CancellationTokenSource();
            watcher = cancel;
            SetStatus("Starting the Riot Client…", true);

            var clock = Stopwatch.StartNew();
            int formPolls = 0;
            bool asked = false;
            while (clock.Elapsed < SignInTimeout)
            {
                LiveState state = await RiotClient.QueryAsync();
                if (cancel.IsCancellationRequested) return;
                if (state.Authenticated)
                {
                    await OnSignedInAsync(state, launched, cancel.Token);
                    return;
                }

                formPolls = state.Reachable && state.SessionType == "needs_credentials" ? formPolls + 1 : 0;
                if (!asked && formPolls >= (launched.Restored ? ExpiredAfterPolls : 1))
                {
                    asked = true;
                    if (launched.Restored)
                    {
                        Log.Write("The saved sign-in of " + launched.Target.DisplayName + " was not accepted");
                        SetStatus("The saved sign-in for " + launched.Target.DisplayName + " has expired. " + TickReminder, true);
                    }
                    else
                    {
                        SetStatus(TickReminder, true);
                    }
                }

                try
                {
                    await Task.Delay(1000, cancel.Token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
            SetStatus("Stopped waiting for a sign-in.", false);
        }

        async Task OnSignedInAsync(LiveState state, Outcome launched, CancellationToken cancel)
        {
            // The session file is written a moment after the client reports the sign-in.
            bool persisted = state.PersistLogin;
            for (int attempt = 0; attempt < 10 && !persisted; attempt++)
            {
                persisted = SessionStore.Inspect(RiotClient.DataDir).HasSession;
                if (!persisted) await Task.Delay(500);
            }
            if (cancel.IsCancellationRequested) return;

            VaultData data = vault.Data;
            if (!persisted && data.Find(state.Puuid) == null)
            {
                ShowBanner("Signed in as " + RiotId(state) + " without “Stay signed in”, so there is nothing to save. "
                    + "Sign out in the Riot Client, then sign in again with the box ticked.", false);
                SetStatus(IdleStatus, false);
                return;
            }

            Account account = Upsert(state);
            data.LiveId = account.Id;
            vault.Save();
            Render();
            Log.Write("Signed in: " + account.DisplayName + (persisted ? "" : " (not persisted)"));

            if (!persisted)
            {
                ShowBanner("Signed in as " + account.DisplayName + " without “Stay signed in”, so this sign-in cannot be saved. "
                    + "Sign out in the Riot Client, then sign in again with the box ticked.", false);
                SetStatus(IdleStatus, false);
            }
            else if (launched.Target == null)
            {
                SetStatus(account.DisplayName + " added and signed in.", false);
            }
            else if (account != launched.Target)
            {
                ShowBanner("Signed in as " + account.DisplayName + ", not " + launched.Target.DisplayName + ".", false);
                SetStatus(IdleStatus, false);
            }
            else
            {
                SetStatus("Signed in as " + account.DisplayName + ".", false);
                await Task.Delay(1500);
                if (!cancel.IsCancellationRequested) Close();
            }
        }

        async Task RemoveAsync(Account account)
        {
            await RunAsync("Checking the Riot Client…", async () =>
            {
                await SyncLiveAsync();
                bool signedIn = vault.Data.Find(vault.Data.LiveId) == account;
                string question = signedIn
                    ? account.DisplayName + " is signed in right now.\n\nRemove it and sign it out? League and the Riot Client will be closed."
                    : "Remove " + account.DisplayName + "?\n\nIts saved sign-in is deleted. To add it back you would sign in again.";
                if (MessageBox.Show(this, question, App.Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                    return new Outcome();

                if (signedIn)
                {
                    string blocked = await RiotClient.BlockReasonAsync();
                    if (blocked != null)
                    {
                        ShowBanner(blocked, true);
                        return new Outcome();
                    }
                    SetStatus("Closing the Riot Client…", true);
                    await RiotClient.StopAsync();
                    SessionStore.SignOut(RiotClient.DataDir);
                    vault.Data.LiveId = null;
                }

                vault.Data.Accounts.Remove(account);
                vault.Save();
                Log.Write("Removed " + account.DisplayName);
                return new Outcome { Status = account.DisplayName + " removed." };
            });
        }

        /// <summary>
        /// Brings the vault in line with the Riot Client: which account is signed in, its current Riot ID,
        /// and any account that was signed in outside the switcher with "Stay signed in".
        /// </summary>
        async Task<LiveState> SyncLiveAsync()
        {
            LiveState state = await RiotClient.QueryAsync();
            SessionInspection file = SessionStore.Inspect(RiotClient.DataDir);
            VaultData data = vault.Data;
            string unsaved = null;

            if (state.Authenticated)
            {
                bool persisted = state.PersistLogin || file.HasSession;
                Account known = data.Find(state.Puuid);
                if (persisted || known != null) data.LiveId = Upsert(state).Id;
                else data.LiveId = null;
                if (!persisted && (known == null || known.Session == null)) unsaved = RiotId(state);
            }
            else if (!file.HasSession)
            {
                data.LiveId = null;
            }
            else if (file.Puuid != null)
            {
                if (data.Find(file.Puuid) == null)
                    data.Accounts.Add(new Account { Id = file.Puuid, AddedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
                data.LiveId = file.Puuid;
            }
            // Otherwise a session is stored but its owner cannot be read from the file:
            // keep the account the last switch recorded.

            vault.Save();
            Render();
            if (unsaved != null)
                ShowBanner("You are signed in as " + unsaved + " without “Stay signed in”, so this sign-in cannot be saved. "
                    + "Use “Add account” and tick the box when you sign in.", false);
            return state;
        }

        Account Upsert(LiveState state)
        {
            Account account = vault.Data.Find(state.Puuid);
            if (account == null)
            {
                account = new Account { Id = state.Puuid, AddedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
                vault.Data.Accounts.Add(account);
            }
            if (!string.IsNullOrEmpty(state.GameName))
            {
                account.GameName = state.GameName;
                account.TagLine = state.TagLine;
            }
            if (state.Platform != null) account.Platform = state.Platform;
            return account;
        }

        static string RiotId(LiveState state)
        {
            return string.IsNullOrEmpty(state.GameName) ? "this account" : state.GameName + "#" + state.TagLine;
        }

        /// <summary>Runs one operation at a time, with the window locked and failures shown in the banner.</summary>
        async Task<Outcome> RunAsync(string status, Func<Task<Outcome>> operation)
        {
            if (busy) return null;
            busy = true;
            if (watcher != null) watcher.Cancel();
            Banner.Visibility = Visibility.Collapsed;
            SetStatus(status, true);
            AddButton.IsEnabled = AccountList.IsEnabled = false;

            Outcome outcome = null;
            try
            {
                outcome = await operation();
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException
                || e is CryptographicException || e is HelloException || e is Win32Exception || e is COMException)
            {
                Log.Write("Failed: " + e);
                ShowBanner(e.Message, true);
            }
            finally
            {
                busy = false;
                AddButton.IsEnabled = AccountList.IsEnabled = true;
                Render();
                SetStatus(outcome != null && outcome.Status != null ? outcome.Status : IdleStatus, false);
            }
            return outcome;
        }

        // ---- presentation ----------------------------------------------------------------------

        void Render()
        {
            var rows = vault.Data.Accounts.OrderBy(account => account.AddedUtc).Select(ToRow).ToList();
            AccountList.ItemsSource = rows;
            EmptyHint.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        AccountRow ToRow(Account account)
        {
            bool signedIn = string.Equals(account.Id, vault.Data.LiveId, StringComparison.OrdinalIgnoreCase);
            bool needsSignIn = !signedIn && account.Session == null;
            string region = (account.Platform ?? "").TrimEnd("0123456789".ToCharArray());
            string name = string.IsNullOrEmpty(account.GameName) ? account.DisplayName : account.GameName;

            return new AccountRow
            {
                Id = account.Id,
                Name = name,
                Tag = string.IsNullOrEmpty(account.GameName) || string.IsNullOrEmpty(account.TagLine) ? "" : " #" + account.TagLine,
                Detail = string.Join(" · ", new[] { account.Label, region }.Where(part => !string.IsNullOrEmpty(part))),
                Chip = signedIn ? "Signed in" : needsSignIn ? "Sign-in needed" : null,
                ChipBrush = (Brush)FindResource(signedIn ? "Ok" : "Warn"),
                Initial = name.Substring(0, 1).ToUpperInvariant(),
                AvatarBrush = new SolidColorBrush(AvatarColors[account.Id.Aggregate(17u, (hash, c) => hash * 31 + c) % AvatarColors.Length]),
                Hint = signedIn ? "Open League with this account"
                    : needsSignIn ? "Open the Riot sign-in screen for this account"
                    : "Switch to this account",
            };
        }

        void SetStatus(string text, bool working)
        {
            Status.Text = text;
            Progress.Visibility = working ? Visibility.Visible : Visibility.Hidden;
        }

        void ShowBanner(string text, bool error)
        {
            Color color = ((SolidColorBrush)FindResource(error ? "Accent" : "Warn")).Color;
            Banner.Background = new SolidColorBrush(Color.FromArgb(0x30, color.R, color.G, color.B));
            Banner.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, color.R, color.G, color.B));
            BannerText.Text = text;
            Banner.Visibility = Visibility.Visible;
        }

        void OnMenuClick(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            Account account = vault.Data.Find((string)button.Tag);
            if (account == null) return;

            var rename = new MenuItem { Header = "Rename label…" };
            rename.Click += (s, a) => RenameLabel(account);
            var remove = new MenuItem { Header = "Remove…" };
            remove.Click += async (s, a) => await RemoveAsync(account);

            var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
            menu.Items.Add(rename);
            menu.Items.Add(remove);
            menu.IsOpen = true;
        }

        void RenameLabel(Account account)
        {
            var box = new TextBox
            {
                Text = account.Label ?? "",
                MaxLength = 40,
                Padding = new Thickness(6, 5, 6, 5),
                Background = (Brush)FindResource("Card"),
                Foreground = (Brush)FindResource("Fg"),
                BorderBrush = (Brush)FindResource("Line"),
                CaretBrush = (Brush)FindResource("Fg"),
            };
            var save = new Button { Content = "Save", IsDefault = true, MinWidth = 80, Margin = new Thickness(8, 0, 0, 0), Style = (Style)FindResource("FlatButton") };
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Style = (Style)FindResource("FlatButton") };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            buttons.Children.Add(cancel);
            buttons.Children.Add(save);

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = "Label for " + account.DisplayName + " (for example “Main” or “Smurf”)", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
            panel.Children.Add(box);
            panel.Children.Add(buttons);

            var dialog = new Window
            {
                Title = "Rename label",
                Owner = this,
                Content = panel,
                Width = 340,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Background,
                Foreground = Foreground,
                FontFamily = FontFamily,
                FontSize = FontSize,
            };
            dialog.SourceInitialized += (s, e) => App.UseDarkTitleBar(dialog);
            dialog.Loaded += (s, e) =>
            {
                box.Focus();
                box.SelectAll();
            };
            save.Click += (s, e) => dialog.DialogResult = true;
            if (dialog.ShowDialog() != true) return;

            string label = box.Text.Trim();
            account.Label = label.Length == 0 ? null : label;
            vault.Save();
            Render();
        }
    }
}
