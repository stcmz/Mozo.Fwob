using System;
using System.Collections.Generic;
using System.Text;

namespace Fwob
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public class LengthAttribute : Attribute
    {
        public int Length { get; private set; }

        public LengthAttribute(int length)
        {
            Length = length;
        }
    }
}
