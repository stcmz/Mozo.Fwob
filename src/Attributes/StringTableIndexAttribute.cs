using System;
using System.Collections.Generic;
using System.Text;

namespace Fwob
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public class StringTableIndexAttribute : Attribute
    {
    }
}
