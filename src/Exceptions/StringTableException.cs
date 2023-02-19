using System;
using System.Runtime.Serialization;

namespace Mozo.Fwob;

[Serializable]
public class StringTableException : Exception
{
    public StringTableException() { }
    public StringTableException(string message) : base(message) { }
    public StringTableException(string message, Exception inner) : base(message, inner) { }
    protected StringTableException(
      SerializationInfo info,
      StreamingContext context) : base(info, context) { }
}
