using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.Serialization;
using CrackLiveXAML;

namespace CrackLiveXAML
{
    internal class RuntimeUpdate
    {
        private ConcurrentDictionary<string, XamlFileMeta> _metaCache = new ConcurrentDictionary<string, XamlFileMeta>();
        private readonly InitialPropertiesContainer _initialPropertiesContainer;

        public IVsSolution CurrentSolution { get; internal set; }

        public RuntimeUpdate(InitialPropertiesContainer initialPropertiesContainer)
        {
            this._initialPropertiesContainer = initialPropertiesContainer;
        }

        internal void DocumentSaved(string filepath, string[] propertiesToReset)
        {
            try
            {
                RuntimeUpdateSender.Send(filepath, propertiesToReset);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Failed to send updates: " + ex.ToString());
            }
        }

        private XamlFileMeta GetCurrentFileMeta(ProjectItem projectItem, bool isSharedProject)
        {
            string str = File.ReadAllText(projectItem.FileNames[0]);
            XDocument xdocument = XDocument.Parse(str);
            return new XamlFileMeta()
            {
                FilePath = projectItem.FileNames[0],
                Hash = XamlHash.Get(str),
                Properties = xdocument.Root.Attributes().Where<XAttribute>((Func<XAttribute, bool>)(a =>
                {
                    if (!a.IsNamespaceDeclaration)
                        return a.Name.NamespaceName == "";
                    return false;
                })).Select<XAttribute, string>((Func<XAttribute, string>)(a => a.Name.LocalName)).ToList<string>()
            };
        }

        //private XamlFileMeta GetOldFileMeta(DTE dte, ProjectItem projectItem)
        //{
        //    string str = (string)null;
        //    Project containingProject = projectItem.ContainingProject;
        //    if (containingProject != null)
        //        str = this.GetMetaFilename(containingProject);
        //    if (str == null || !File.Exists(str))
        //        str = VsUtils.GetAllProjects(dte.Solution).Select<Project, string>((Func<Project, string>)(p => this.GetMetaFilename(p))).Where<string>((Func<string, bool>)(fname =>
        //        {
        //            if (fname != null)
        //                return File.Exists(fname);
        //            return false;
        //        })).OrderByDescending<string, DateTime>((Func<string, DateTime>)(fname => new FileInfo(fname).LastWriteTime)).FirstOrDefault<string>();
        //    if (str == null)
        //    {
        //        int num = (int)MessageBox.Show("Unable to find meta filename location. Make sure you have installed LiveXAML into this project.");
        //        throw new FileNotFoundException(str);
        //    }
        //    using (FileStream fileStream = File.OpenRead(str))
        //        return (new XmlSerializer(typeof(XamlProjectMeta)).Deserialize((Stream)fileStream) as XamlProjectMeta).Files.FirstOrDefault<XamlFileMeta>((Func<XamlFileMeta, bool>)(f => string.Equals(f.FilePath, projectItem.GetProjectItemFilename(), StringComparison.InvariantCultureIgnoreCase)));
        //}

        private string GetMetaFilename(Project project)
        {
            try
            {
                TryResult<string> tryResult1 = Try.Get<string>((Func<string>)(() => RuntimeUpdate.GetObjPathVanilla(project)));
                TryResult<string> tryResult2 = Try.Get<string>((Func<string>)(() => RuntimeUpdate.GetProjectFullPath(project)));
                if (!tryResult1.HasNonNullValue)
                    tryResult1 = Try.Get<string>((Func<string>)(() => this.GetObjPathNetStandard(project)));
                if (tryResult1.HasNonNullValue && tryResult2.HasNonNullValue)
                    return Path.Combine(Path.IsPathRooted(tryResult1.Value) ? tryResult1.Value : Path.Combine(tryResult2.Value, tryResult1.Value), "xamarinlive.meta");
                if (tryResult2.HasNonNullValue)
                    return RuntimeUpdate.FindMetaFileInProjectFolder(tryResult2.Value);
                return (string)null;
            }
            catch
            {
                return (string)null;
            }
        }

        private static string FindMetaFileInProjectFolder(string fullPath)
        {
            return ((IEnumerable<string>)Directory.GetFiles(fullPath, "xamarinlive.meta", SearchOption.AllDirectories)).FirstOrDefault<string>();
        }

        private static string GetProjectFullPath(Project project)
        {
            return project.Properties.Item("FullPath").ToString();
        }

        private string GetObjPathNetStandard(Project project)
        {
            IVsBuildPropertyStorage buildPropertyStorage = this.GetBuildPropertyStorage(project);
            if (buildPropertyStorage != null)
                return this.GetBuildProperty("IntermediateOutputPath", buildPropertyStorage);
            return (string)null;
        }

        private static string GetObjPathVanilla(Project project)
        {
            return project?.ConfigurationManager?.ActiveConfiguration?.Properties?.Item("IntermediatePath")?.ToString();
        }

        private IVsBuildPropertyStorage GetBuildPropertyStorage(Project project)
        {
            if (this.CurrentSolution == null)
                return (IVsBuildPropertyStorage)null;
            IVsHierarchy ppHierarchy;
            Marshal.ThrowExceptionForHR(this.CurrentSolution.GetProjectOfUniqueName(project.FullName, out ppHierarchy));
            return ppHierarchy as IVsBuildPropertyStorage;
        }

        private string GetBuildProperty(string key, IVsBuildPropertyStorage Storage)
        {
            string pbstrPropValue;
            int propertyValue = Storage.GetPropertyValue(key, (string)null, 2U, out pbstrPropValue);
            int num = -2147170504;
            if (propertyValue != num)
                Marshal.ThrowExceptionForHR(propertyValue);
            return pbstrPropValue;
        }
    }
}
