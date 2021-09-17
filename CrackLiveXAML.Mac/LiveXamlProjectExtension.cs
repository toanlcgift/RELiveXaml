using MonoDevelop.Core;
using MonoDevelop.Debugger;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Projects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LiveXAML
{
    public class LiveXamlProjectExtension : SolutionExtension
    {
        private static bool _isInitialized = false;
        private DateTime _mostRecentSaveHandlerTime;
        private RuntimeUpdate _runtimeUpdate;
        private InitialPropertiesContainer _initialPropertiesContainer = new InitialPropertiesContainer();

        public static bool FirstTime
        {
            get
            {
                return (bool)PropertyService.Get<bool>("LiveXAML_FirstTime", true);
            }
            set
            {
                PropertyService.Set("LiveXAML_FirstTime", (object)value);
                PropertyService.SaveProperties();
            }
        }

        public LiveXamlProjectExtension() : base()
        {
            if (LiveXamlProjectExtension._isInitialized)
                return;
            LiveXamlProjectExtension._isInitialized = true;
            this._runtimeUpdate = new RuntimeUpdate(this._initialPropertiesContainer);
            FileService.FileChanged += this.FileService_FileChanged;
            Server.Start();
            IdeApp.Workspace.SolutionLoaded += ((EventHandler<SolutionEventArgs>)((sender, e) =>
             {
                 using (IEnumerator<Solution> enumerator = IdeApp.Workspace.GetAllSolutions().GetEnumerator())
                 {
                     while (((IEnumerator)enumerator).MoveNext())
                     {
                  // ISSUE: method pointer
                  enumerator.Current.FileAddedToProject += FileAddedToProject;
                     }
                 }
                 this.ReloadXamlFiles();
             }));
        }

        private void FileService_FileChanged(object sender, FileEventArgs e)
        {
            FileEventInfo fileEventInfo = ((IEnumerable<FileEventInfo>)e).FirstOrDefault<FileEventInfo>();
            FilePath fileName1 = fileEventInfo.FileName;
            // ISSUE: explicit reference operation
            if (!(((FilePath)fileName1).Extension == ".xaml"))
                return;
            if ((DateTime.Now - this._mostRecentSaveHandlerTime).TotalSeconds < 0.5)
            {
            }
            else
            {
                this._mostRecentSaveHandlerTime = DateTime.Now;
                if (!DebuggingService.IsDebugging)
                 return;
                Document activeDocument = IdeApp.Workbench.ActiveDocument;
                FilePath filePath = fileEventInfo.FileName;
                // ISSUE: explicit reference operation
                filePath = ((FilePath)filePath).CanonicalPath;
                // ISSUE: explicit reference operation
                // ISSUE: variable of a reference type
                FilePath local = filePath;
                FilePath fileName2 = activeDocument.FileName;
                // ISSUE: explicit reference operation
                FilePath canonicalPath1 = ((FilePath)fileName2).CanonicalPath;
                if (!((FilePath)local).Equals(canonicalPath1))
                {
                    //Log.LogInfo("File changed is not the active document. Skipping update.");
                }
                else
                {
                    // ISSUE: explicit reference operation
                    List<FilePath> list = IdeApp.Workspace.GetAllSolutionItems().SelectMany<SolutionItem, FilePath>((Func<SolutionItem, IEnumerable<FilePath>>)(si => si.GetItemFiles(true))).Distinct<FilePath>().Where<FilePath>((Func<FilePath, bool>)(fp => string.Equals(((FilePath)fp).Extension, ".xaml", StringComparison.InvariantCultureIgnoreCase))).ToList<FilePath>();

                    filePath = activeDocument.FileName;
                    // ISSUE: explicit reference operation
                    FilePath canonicalPath2 = ((FilePath)filePath).CanonicalPath;
                    string[] propertiesForFile = this._initialPropertiesContainer.GetPropertiesForFile(FilePath.Build(canonicalPath2));
                    this._initialPropertiesContainer.AddFile(FilePath.Build(canonicalPath2));
                    this._runtimeUpdate.DocumentSaved(FilePath.Build(canonicalPath2), propertiesForFile);
                }
            }
        }

        private void ReloadXamlFiles()
        {
            Task.Run((Action)(() =>
           {
               using (IEnumerator<ProjectFile> enumerator = IdeApp.Workspace.GetAllProjects().SelectMany<Project, ProjectFile>((Func<Project, IEnumerable<ProjectFile>>)(p => ((IEnumerable<ProjectFile>)p.Files).AsEnumerable<ProjectFile>())).GetEnumerator())
               {
                   while (((IEnumerator)enumerator).MoveNext())
                   {
                       ProjectFile current = enumerator.Current;
                       if (string.Equals(Path.GetExtension(current.Name), ".xaml", StringComparison.InvariantCultureIgnoreCase))
                           this._initialPropertiesContainer.AddFile(FilePath.Build(current.FilePath));
                   }
               }
           }));
        }

        private void FileAddedToProject(object sender, ProjectFileEventArgs e)
        {
            this.ReloadXamlFiles();
        }
    }
}
