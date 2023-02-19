using System;
using System.Runtime.Serialization;

namespace Mozo.Fwob;

[Serializable]
public class KeyOrderingException : Exception
{
    public KeyOrderingException() { }
    public KeyOrderingException(string message) : base(message) { }
    public KeyOrderingException(string message, Exception inner) : base(message, inner) { }
    protected KeyOrderingException(
      SerializationInfo info,
      StreamingContext context) : base(info, context) { }
}
