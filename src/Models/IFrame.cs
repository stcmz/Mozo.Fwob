using System;

namespace Mozo.Fwob.Models;

public interface IFrame<TKey>
    where TKey : struct, IComparable<TKey>
{
    TKey Key { get; }
}
