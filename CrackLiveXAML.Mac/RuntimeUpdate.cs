using MonoDevelop.Core;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Projects;
using MonoDevelop.Projects.MSBuild;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace LiveXAML
{
  internal class RuntimeUpdate
  {
    private ConcurrentDictionary<string, XamlFileMeta> _metaCache = new ConcurrentDictionary<string, XamlFileMeta>();
    private InitialPropertiesContainer _initialPropertiesContainer;

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
        //Log.LogError("Failed to send updates: " + ex.ToString());
      }
    }

    private XamlFileMeta GetCurrentFileMeta(Document document, bool isSharedProject)
    {
      string str1 = File.ReadAllText(FilePath.Build(document.FileName));
      XDocument xdocument = XDocument.Parse(str1);
      XamlFileMeta xamlFileMeta = new XamlFileMeta();
      FilePath fileName = document.FileName;
      // ISSUE: explicit reference operation
      string str2 = FilePath.Build(((FilePath) @fileName).CanonicalPath);
      xamlFileMeta.FilePath = str2;
      string str3 = XamlHash.Get(str1);
      xamlFileMeta.Hash = str3;
      List<string> list = xdocument.Root.Attributes().Where<XAttribute>((Func<XAttribute, bool>) (a =>
      {
        if (!a.IsNamespaceDeclaration)
          return a.Name.NamespaceName == "";
        return false;
      })).Select<XAttribute, string>((Func<XAttribute, string>) (a => a.Name.LocalName)).ToList<string>();
      xamlFileMeta.Properties = list;
      return xamlFileMeta;
    }

    private static XamlFileMeta GetOldFileMeta(Document document)
    {
      string str = (string) null;
      Document document1 = document;
      Project project = document1 != null ? ((DocumentContext) document1).Project : (Project) null;
      if (project != null)
        str = RuntimeUpdate.GetMetaFilename(project);
      Solution parentSolution = ((SolutionFolderItem) project).ParentSolution;
      if (str == null)
        str = parentSolution.GetAllProjects().Select<Project, string>((Func<Project, string>) (p => RuntimeUpdate.GetMetaFilename(p))).Where<string>((Func<string, bool>) (fname =>
        {
          if (fname != null)
            return File.Exists(fname);
          return false;
        })).OrderByDescending<string, DateTime>((Func<string, DateTime>) (fname => new FileInfo(fname).LastWriteTime)).FirstOrDefault<string>();
      if (str == null)
        throw new FileNotFoundException(str);
      using (FileStream fileStream = File.OpenRead(str))
        return (new XmlSerializer(typeof (XamlProjectMeta)).Deserialize((Stream) fileStream) as XamlProjectMeta).Files.FirstOrDefault<XamlFileMeta>((Func<XamlFileMeta, bool>) (f =>
        {
          string filePath = f.FilePath;
          FilePath fileName = document.FileName;
          // ISSUE: explicit reference operation
          FilePath canonicalPath = ((FilePath) @fileName).CanonicalPath;
          return RuntimeUpdate.IsSamePath(filePath, canonicalPath);
        }));
    }

    private static bool IsSamePath(string a, FilePath b)
    {
      return string.Equals(a, FilePath.Build(b), StringComparison.InvariantCultureIgnoreCase);
    }

    private static string GetMetaFilename(Project project)
    {
      try
      {
        IMSBuildPropertyEvaluated property = ((IMSBuildPropertyGroupEvaluated) project.MSBuildProject.EvaluatedProperties).GetProperty("IntermediateOutputPath");
        if (property == null)
          return (string) null;
        string str = (string) property.GetValue<string>();
        if (str == null)
          return (string) null;
        string path2 = str.Replace('\\', '/');
        FilePath filePath = ((SolutionItem) project).FileName;
        // ISSUE: explicit reference operation
        filePath = ((FilePath) @filePath).ParentDirectory;
        // ISSUE: explicit reference operation
        string path = Path.Combine(FilePath.Build(((FilePath) @filePath).CanonicalPath), path2, "xamarinlive.meta");
        if (!File.Exists(path))
          return (string) null;
        return path;
      }
      catch
      {
        return (string) null;
      }
    }
  }
}
