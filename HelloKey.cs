using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;

namespace RiotAccountSwitcher
{
    public sealed class HelloException : Exception
    {
        public HelloException(string message) : base(message) { }
    }

    /// <summary>
    /// Derives the vault key from a Windows Hello credential. The private key never leaves Windows
    /// (TPM-backed where available) and signs only after a PIN, fingerprint or face gesture. Its RSA
    /// PKCS#1 v1.5 signature over a fixed challenge is deterministic, so hashing it yields the same
    /// key on every unlock without that key ever being stored.
    /// </summary>
    public static class HelloKey
    {
        const string CredentialName = "RiotAccountSwitcher";

        /// <returns>A 32-byte key, or null when the user dismissed the prompt.</returns>
        public static async Task<byte[]> DeriveAsync(byte[] challenge)
        {
            if (!await KeyCredentialManager.IsSupportedAsync())
                throw new HelloException("Windows Hello is not set up on this PC. Add a PIN in Settings > Accounts > Sign-in options.");

            using (var prompt = new CancellationTokenSource())
            {
                Task focus = FocusPromptAsync(prompt.Token);
                try
                {
                    KeyCredentialRetrievalResult credential = await KeyCredentialManager.OpenAsync(CredentialName);
                    if (credential.Status == KeyCredentialStatus.NotFound)
                        credential = await KeyCredentialManager.RequestCreateAsync(CredentialName, KeyCredentialCreationOption.FailIfExists);
                    if (credential.Status == KeyCredentialStatus.UserCanceled) return null;
                    if (credential.Status != KeyCredentialStatus.Success)
                        throw new HelloException("Windows Hello could not provide the key (" + credential.Status + ").");

                    KeyCredentialOperationResult signed =
                        await credential.Credential.RequestSignAsync(CryptographicBuffer.CreateFromByteArray(challenge));
                    if (signed.Status == KeyCredentialStatus.UserCanceled) return null;
                    if (signed.Status != KeyCredentialStatus.Success)
                        throw new HelloException("Windows Hello did not confirm it is you (" + signed.Status + ").");

                    CryptographicBuffer.CopyToByteArray(signed.Result, out byte[] signature);
                    using (var sha = SHA256.Create()) return sha.ComputeHash(signature);
                }
                finally
                {
                    prompt.Cancel();
                    await focus;
                }
            }
        }

        /// <summary>
        /// The Hello dialog belongs to a system process and can open behind the window that asked for it.
        /// Bring each new dialog to the front once.
        /// </summary>
        static async Task FocusPromptAsync(CancellationToken cancel)
        {
            IntPtr focused = IntPtr.Zero;
            while (!cancel.IsCancellationRequested)
            {
                IntPtr dialog = FindWindow("Credential Dialog Xaml Host", null);
                if (dialog != IntPtr.Zero && dialog != focused)
                {
                    SetForegroundWindow(dialog);
                    focused = dialog;
                }
                try
                {
                    await Task.Delay(200, cancel);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr window);
    }
}
