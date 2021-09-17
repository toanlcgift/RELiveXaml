using System;
using System.Collections.Generic;

namespace CrackLiveXAML
{
  [Serializable]
  public class XamlFileMeta
  {
    public string FilePath { get; set; }

    public string Hash { get; set; }

    public List<string> Properties { get; set; } = new List<string>();
  }
}
