using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Wordlist & Log Viewer")]
[assembly: AssemblyDescription("Read-only viewer and exact-search tool for extremely large text, wordlist, dictionary, and log files")]
[assembly: AssemblyCompany("Wordlist Viewer")]
[assembly: AssemblyProduct("Wordlist & Log Viewer")]
[assembly: AssemblyCopyright("2026")]
[assembly: AssemblyVersion("2.3.2.0")]
[assembly: AssemblyFileVersion("2.3.2.0")]

namespace WordlistViewer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Localization.Initialize();

            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                ShowFatalError(e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Exception exception = e.ExceptionObject as Exception;
                if (exception != null)
                {
                    ShowFatalError(exception);
                }
            };

            string initialFile = null;
            if (args != null && args.Length > 0 && File.Exists(args[0]))
            {
                initialFile = Path.GetFullPath(args[0]);
            }

            FullSearchResultStore.CleanupOrphans();
            Application.Run(new MainFormV2(initialFile));
        }

        private static void ShowFatalError(Exception exception)
        {
            try
            {
                MessageBox.Show(
                    Localization.Format("fatal.unexpected", exception.Message),
                    "Wordlist Viewer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
                // The process is already failing; there is no safe recovery path here.
            }
        }
    }
}
