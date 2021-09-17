using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace CrackLiveXAML
{
    [Serializable]
    internal class MalformedXamlException : Exception
    {
        public MalformedXamlException()
        {
        }

        public MalformedXamlException(string message)
          : base(message)
        {
        }

        public MalformedXamlException(string message, Exception innerException)
          : base(message, innerException)
        {
        }

        protected MalformedXamlException(SerializationInfo info, StreamingContext context)
          : base(info, context)
        {
        }
    }
}
