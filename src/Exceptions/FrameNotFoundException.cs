using System;
using System.Runtime.Serialization;

namespace Fwob
{
    [Serializable]
    public class FrameNotFoundException : Exception
    {
        public FrameNotFoundException() { }
        public FrameNotFoundException(string message) : base(message) { }
        public FrameNotFoundException(string message, Exception inner) : base(message, inner) { }
        protected FrameNotFoundException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
    }
}
