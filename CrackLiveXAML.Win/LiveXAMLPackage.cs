using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Task = System.Threading.Tasks.Task;

namespace CrackLiveXAML
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#2110", "#2112", "1.0", IconResourceID = 2400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(LiveXAMLPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class LiveXAMLPackage : AsyncPackage
    {
        public const string PackageGuidString = "a6fb52bf-5b67-43ed-8c45-7a5ceac081aa";
        private List<FileSystemWatcher> _fileSystemWatchers = new List<FileSystemWatcher>();
        private InitialPropertiesContainer _initialPropertiesContainer = new InitialPropertiesContainer();
        private ConcurrentDictionary<string, DateTime> _changeTimeMap = new ConcurrentDictionary<string, DateTime>();
        private System.Diagnostics.Process _xlServerProcess;
        private RuntimeUpdate _runtimeUpdate;
        public LiveXAMLPackage()
        {
            this._runtimeUpdate = new RuntimeUpdate(this._initialPropertiesContainer);
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await LiveXAML.InitializeAsync(this);
            this._runtimeUpdate.CurrentSolution = (IVsSolution)await this.GetServiceAsync(typeof(SVsSolution));
            this.StartServerProcess();
            DTE dte = await this.GetServiceAsync(typeof(SDTE)) as DTE;
            if (dte == null)
            {
                Logger.WriteLine("dte is null");
            }
            Solution solution = dte != null ? dte.Solution : (Solution)null;
            if (solution != null)
            {
                var directory = Path.GetDirectoryName(solution.FullName);

                try
                {
                    Logger.WriteLine("Project path " + directory);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        this.CreateFileSystemWatcherForPath(directory);
                        this.ReadInitialPropertiesOfXamlFiles(directory);
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(ex.Message);
                }
            }
        }

        private void ReadInitialPropertiesOfXamlFiles(string projectPath)
        {
            _ = System.Threading.Tasks.Task.Run((() =>
              {
                  try
                  {
                      foreach (string enumerateFile in Directory.EnumerateFiles(projectPath, "*.xaml", SearchOption.AllDirectories))
                          this._initialPropertiesContainer.AddFile(enumerateFile);
                  }
                  catch (Exception ex)
                  {
                      Logger.WriteLine("Reading initial properties failed: " + (object)ex);
                  }
              }));
        }

        private void CreateFileSystemWatcherForPath(string path)
        {
            FileSystemWatcher fileSystemWatcher = new FileSystemWatcher()
            {
                Path = path,
                Filter = "*.xaml",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Attributes | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.LastAccess | NotifyFilters.CreationTime | NotifyFilters.Security,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };
            fileSystemWatcher.Changed += new FileSystemEventHandler(this.Watcher_Changed);
            this._fileSystemWatchers.Add(fileSystemWatcher);
            Logger.WriteLine("Watching " + path);
        }

        private async void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);
            if (e.ChangeType != WatcherChangeTypes.Changed)
                return;
            Logger.WriteLine("Changed called with " + (object)e.ChangeType + " on " + e.FullPath);
            try
            {
                Solution solution = (await this.GetServiceAsync(typeof(SDTE)) as DTE).Solution;

                if (e.ChangeType != WatcherChangeTypes.Changed || solution == null)
                    return;
                ProjectItem projectItem = solution.FindProjectItem(e.FullPath);
                Logger.WriteLine("Found project item = " + (object)projectItem);
                if (projectItem != null)
                    await this.OnDocumentSavedAsync(projectItem);
            }
            catch (Exception ex)
            {
                Logger.WriteLine(ex.ToString());
            }
        }

        private async Task OnDocumentSavedAsync(ProjectItem projectItem)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);
            if (projectItem == null)
                return;
            string projectItemFilename = projectItem.FileCount > 0 ? projectItem.FileNames[0] : string.Empty;
            DateTime dateTime;
            if (!projectItemFilename.EndsWith(".xaml", StringComparison.InvariantCultureIgnoreCase) || this._changeTimeMap.TryGetValue(projectItemFilename, out dateTime) && DateTime.Now - dateTime < TimeSpan.FromMilliseconds(500.0))
                return;
            this._changeTimeMap[projectItemFilename] = DateTime.Now;
            string[] propertiesForFile = this._initialPropertiesContainer.GetPropertiesForFile(projectItemFilename);
            this._initialPropertiesContainer.AddFile(projectItemFilename);
            Logger.WriteLine("Document saved " + projectItemFilename);
            if ((await this.GetServiceAsync(typeof(SDTE)) as DTE).Debugger.DebuggedProcesses.Count == 0)
                return;

            {
                try
                {
                    RuntimeUpdateSender.Send(projectItemFilename, propertiesForFile);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("Unable to send updates: " + (object)ex);
                    int num3 = (int)MessageBox.Show("Failed to send updates" + Environment.NewLine + (object)ex);
                }
            }
        }


        private void StartServerProcess()
        {
            Logger.WriteLine("Starting server process");
            string path = Path.Combine(Path.GetDirectoryName(this.GetType().Assembly.Location), "xlserver.exe");
            if (!File.Exists(path))
            {
                int num = (int)MessageBox.Show("Can't find xlserver.exe" + Environment.NewLine + "Make sure that your antivirus didn't block it when installing the LiveXAML Visual Studio extension" + Environment.NewLine + "Usually it is located somewhere under in c:\\Users\\{USER}\\AppData\\Local\\Microsoft\\VisualStudio\\");
            }
            else
            {
                this._xlServerProcess = System.Diagnostics.Process.Start(new ProcessStartInfo()
                {
                    FileName = path,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                });
                Logger.WriteLine("Started server process");
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (this._xlServerProcess == null || this._xlServerProcess.HasExited)
                    return;
                this._xlServerProcess?.Kill();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

    }
}
