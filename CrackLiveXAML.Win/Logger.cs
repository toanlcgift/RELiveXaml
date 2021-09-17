using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrackLiveXAML
{
    public static class Logger
    {
        internal static void WriteLine(string v)
        {
            File.AppendAllLines(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "logCrackLiveXAML.txt"), new[] { v });
        }
    }
}
