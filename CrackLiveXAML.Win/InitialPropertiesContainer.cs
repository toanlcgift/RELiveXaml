using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CrackLiveXAML
{
    internal class InitialPropertiesContainer
    {
        private ConcurrentDictionary<string, string[]> _initialPropertiesByFile = new ConcurrentDictionary<string, string[]>();

        public void AddFile(string filepath)
        {
            XDocument xdocument = XDocument.Load(filepath);
            if (xdocument.Root == null)
                return;
            string[] array = xdocument.Root.Attributes().Where<XAttribute>((Func<XAttribute, bool>)(a =>
            {
                if (!a.IsNamespaceDeclaration)
                    return a.Name.NamespaceName == "";
                return false;
            })).Select<XAttribute, string>((Func<XAttribute, string>)(a => a.Name.LocalName)).ToArray<string>();
            this._initialPropertiesByFile[filepath] = array;
        }

        public string[] GetPropertiesForFile(string filepath)
        {
            string[] strArray;
            if (this._initialPropertiesByFile.TryGetValue(filepath, out strArray))
                return strArray;
            Logger.WriteLine("GetPropertiesForFile couldn't find file: " + filepath);
            return new string[0];
        }
    }
}
