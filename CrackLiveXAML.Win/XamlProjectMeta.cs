using System;
using System.Collections.Generic;

namespace CrackLiveXAML
{
  [Serializable]
  public class XamlProjectMeta
  {
    public List<XamlFileMeta> Files { get; } = new List<XamlFileMeta>();
  }
}
