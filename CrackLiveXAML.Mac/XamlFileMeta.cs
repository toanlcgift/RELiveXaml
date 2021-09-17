using System;
using System.Collections.Generic;

namespace LiveXAML
{
  [Serializable]
  public class XamlFileMeta
  {
    public List<string> Properties { get; set; } = new List<string>();

    public string FilePath { get; set; }

    public string Hash { get; set; }
  }
}
