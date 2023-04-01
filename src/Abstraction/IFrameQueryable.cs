using System;
using System.Collections.Generic;

namespace Mozo.Fwob.Abstraction;

public interface IFrameQueryable<out TFrame, TKey> : IEnumerable<TFrame>
    where TFrame : class
    where TKey : struct, IComparable<TKey>
{
    /// <summary>
    /// Gets the number of <typeparamref name="TFrame"/> instances in the collection.
    /// </summary>
    long FrameCount { get; }

    /// <summary>
    /// Gets the first <typeparamref name="TFrame"/> instance in the collection if exists, null if otherwise.
    /// </summary>
    TFrame? FirstFrame { get; }

    /// <summary>
    /// Gets the last <typeparamref name="TFrame"/> instance in the collection if exists, null if otherwise.
    /// </summary>
    TFrame? LastFrame { get; }

    /// <summary>
    /// Returns a <typeparamref name="TFrame"/> instance at the given index.
    /// <br/>
    /// Should run in constant time.
    /// </summary>
    /// <param name="index">An integer representing the position of the <typeparamref name="TFrame"/> instance to be retrieved.</param>
    /// <returns>The <typeparamref name="TFrame"/> instance at the given index.</returns>
    TFrame? this[long index] { get; }

    /// <summary>
    /// Returns the lower bound index for the given <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The <typeparamref name="TKey"/> to query.</param>
    /// <returns>The lower bound index for the given <paramref name="key"/>.</returns>
    long LowerBoundOf(TKey key);

    /// <summary>
    /// Returns the upper bound index for the given <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The <typeparamref name="TKey"/> to query.</param>
    /// <returns>The upper bound index for the given <paramref name="key"/>.</returns>
    long UpperBoundOf(TKey key);

    /// <summary>
    /// Returns the lower and upper bound index for the given <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The <typeparamref name="TKey"/> to query.</param>
    /// <returns>The lower and upper bound index for the given <paramref name="key"/>.</returns>
    (long LowerBound, long UpperBound) EqualRangeOf(TKey key);

    /// <summary>
    /// Returns the <typeparamref name="TKey"/> of the <typeparamref name="TFrame"/> instance at the given index.
    /// <br/>
    /// Should run in constant time.
    /// </summary>
    /// <param name="index">An integer representing the position of the <typeparamref name="TFrame"/> instance whose key is to be retrieved.</param>
    /// <returns>The <typeparamref name="TKey"/> of the <typeparamref name="TFrame"/> instance at the given index.</returns>
    TKey? GetKeyAt(long index);

    /// <summary>
    /// Returns a <typeparamref name="TFrame"/> instance at the given index.
    /// <br/>
    /// Should run in constant time.
    /// </summary>
    /// <param name="index">An integer representing the position of the <typeparamref name="TFrame"/> instance to be retrieved.</param>
    /// <returns>The <typeparamref name="TFrame"/> instance at the given index.</returns>
    TFrame? GetFrameAt(long index);

    /// <summary>
    /// Returns an <see cref="IEnumerable{TFrame}"/> for the given query <paramref name="keys"/>.
    /// The <paramref name="keys"/> must be ordered or a <see cref="ArgumentException"/> will be thrown before retrieving any data.
    /// <br/>
    /// Should run in O(KlogN) time or better, where K is the number of query keys and N is the number of <typeparamref name="TFrame"/> instances in the collection.
    /// </summary>
    /// <param name="keys">The ordered query keys.</param>
    /// <returns>The <typeparamref name="TFrame"/> instances in the order of the query <paramref name="keys"/></returns>
    IEnumerable<TFrame> GetFrames(params TKey[] keys);

    /// <summary>
    /// Returns an <see cref="IEnumerable{TFrame}"/> for the given query <paramref name="keys"/>.
    /// The <paramref name="keys"/> must be ordered or a <see cref="ArgumentException"/> will be thrown before retrieving any data.
    /// <br/>
    /// Should run in O(KlogN) time or better, where K is the number of query keys and N is the number of <typeparamref name="TFrame"/> instances in the collection.
    /// </summary>
    /// <param name="keys">The ordered query keys.</param>
    /// <returns>The <typeparamref name="TFrame"/> instances in the order of the query <paramref name="keys"/></returns>
    IEnumerable<TFrame> GetFrames(IEnumerable<TKey> keys);

    /// <summary>
    /// Returns all <typeparamref name="TFrame"/> instances in the given range [<paramref name="firstKey"/>, <paramref name="lastKey"/>].
    /// <br/>
    /// Should run in O(K + logN) time or better, where K is the number of returned <typeparamref name="TFrame"/> instances and N is the number of <typeparamref name="TFrame"/> instances in the collection.
    /// </summary>
    /// <param name="firstKey">The lower bound of the query.</param>
    /// <param name="lastKey">The higher bound of the query.</param>
    /// <returns>The <typeparamref name="TFrame"/> instances whose keys are in the given range.</returns>
    IEnumerable<TFrame> GetFramesBetween(TKey firstKey, TKey lastKey);

    /// <summary>
    /// Returns all <typeparamref name="TFrame"/> instances for key less than or equal to <paramref name="lastKey"/>.
    /// <br/>
    /// Should run in O(K + logN) time or better, where K is the number of returned <typeparamref name="TFrame"/> instances and N is the number of <typeparamref name="TFrame"/> instances in the collection.
    /// </summary>
    /// <param name="lastKey">The higher bound of the query.</param>
    /// <returns>The <typeparamref name="TFrame"/> instances whose keys are in the given range.</returns>
    IEnumerable<TFrame> GetFramesBefore(TKey lastKey);

    /// <summary>
    /// Returns all <typeparamref name="TFrame"/> instances for key greater than or equal to <paramref name="firstKey"/>.
    /// <br/>
    /// Should run in O(K + logN) time or better, where K is the number of returned <typeparamref name="TFrame"/> instances and N is the number of <typeparamref name="TFrame"/> instances in the collection.
    /// </summary>
    /// <param name="firstKey">The lower bound of the query.</param>
    /// <returns>The <typeparamref name="TFrame"/> instances whose keys are in the given range.</returns>
    IEnumerable<TFrame> GetFramesAfter(TKey firstKey);

    /// <summary>
    /// Returns all <typeparamref name="TFrame"/> instances in the collection.
    /// <br/>
    /// Should run in O(N) time, where N is the number of <typeparamref name="TFrame"/> instances in the collection.
    /// </summary>
    /// <returns></returns>
    IEnumerable<TFrame> GetAllFrames();
}
