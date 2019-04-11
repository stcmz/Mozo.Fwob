using System;
using System.Collections.Generic;
using System.Text;

namespace Fwob.Models
{
    public interface IStringTable
    {
        IReadOnlyList<string> Strings { get; }

        int StringCount { get; }

        string GetString(int index);

        int GetIndex(string str);

        int AppendString(string str);

        bool ContainsString(string str);

        void ClearStrings();
    }
}
