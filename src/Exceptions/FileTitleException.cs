using System;
using System.Runtime.Serialization;

namespace Mozo.Fwob;

[Serializable]
public class FileTitleException : Exception
{
    public FileTitleException() { }
    public FileTitleException(string message) : base(message) { }
    public FileTitleException(string message, Exception inner) : base(message, inner) { }
    protected FileTitleException(
      SerializationInfo info,
      StreamingContext context) : base(info, context) { }
}
