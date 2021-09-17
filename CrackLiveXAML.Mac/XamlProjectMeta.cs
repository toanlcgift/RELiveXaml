using System;
using System.Collections.Generic;

namespace LiveXAML
{
  [Serializable]
  public class XamlProjectMeta
  {
    public List<XamlFileMeta> Files { get; } = new List<XamlFileMeta>();
  }
}
