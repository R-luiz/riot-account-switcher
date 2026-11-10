using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace RiotAccountSwitcher
{
    public partial class App : Application
    {
        public const string Title = "Riot Account Switcher";

        Mutex singleInstance;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Two instances would swap the same Data folder under each other.
            singleInstance = new Mutex(true, @"Local\RiotAccountSwitcher", out bool first);
            if (!first)
            {
                IntPtr existing = FindWindow(null, Title);
                if (existing != IntPtr.Zero)
                {
                    ShowWindow(existing, 9 /* SW_RESTORE */);
                    SetForegroundWindow(existing);
                }
                Shutdown();
                return;
            }

            DispatcherUnhandledException += (sender, args) =>
            {
                Log.Write("Unhandled: " + args.Exception);
                MessageBox.Show(args.Exception.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            new MainWindow().Show();
        }

        /// <summary>Windows draws a light title bar unless the window asks for the dark one.</summary>
        public static void UseDarkTitleBar(Window window)
        {
            int enabled = 1;
            DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, ref enabled, sizeof(int));
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    }
}
