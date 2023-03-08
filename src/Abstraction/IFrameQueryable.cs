using System;
using System.Collections.Generic;

namespace Mozo.Fwob.Abstraction;

public interface IFrameQueryable<TFrame, TKey>
    where TFrame : class, new()
    where TKey : struct, IComparable<TKey>
{
    /// <summary>
    /// Gets the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    long FrameCount { get; }

    /// <summary>
    /// Gets the first <typeparamref name="TFrame"/> instance in the storage if exists, null if otherwise.
    /// </summary>
    TFrame? FirstFrame { get; }

    /// <summary>
    /// Gets the last <typeparamref name="TFrame"/> instance in the storage if exists, null if otherwise.
    /// </summary>
    TFrame? LastFrame { get; }

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
    /// Should run in O(KlogN) time or better, where K is the number of query keys and N is the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    /// <param name="keys">The ordered query keys.</param>
    /// <returns>The <typeparamref name="TFrame"/> instances in the order of the query <paramref name="keys"/></returns>
    IEnumerable<TFrame> GetFrames(params TKey[] keys);

    /// <summary>
    /// Returns an <see cref="IEnumerable{TFrame}"/> for the given query <paramref name="keys"/>.
    /// The <paramref name="keys"/> must be ordered or a <see cref="ArgumentException"/> will be thrown before retrieving any data.
    /// <br/>
    /// Should run in O(KlogN) time or better, where K is the number of query keys and N is the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    /// <param name="keys">The ordered query keys.</param>
    /// <returns>The <typeparamref name="TFrame"/> instances in the order of the query <paramref name="keys"/></returns>
    IEnumerable<TFrame> GetFrames(IEnumerable<TKey> keys);

    /// <summary>
    /// Returns all <typeparamref name="TFrame"/> instances in the given range [<paramref name="firstKey"/>, <paramref name="lastKey"/>].
    /// <br/>
    /// Should run in O(K + logN) time or better, where K is the number of returned <typeparamref name="TFrame"/> instances and N is the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    /// <param name="firstKey">The lower bound of the query.</param>
    /// <param name="lastKey">The higher bound of the query.</param>
    /// <returns>The <typeparamref name="TFrame"/> instances whose keys are in the given range.</returns>
    IEnumerable<TFrame> GetFramesBetween(TKey firstKey, TKey lastKey);

    /// <summary>
    /// Returns all <typeparamref name="TFrame"/> instances for key less than or equal to <paramref name="lastKey"/>.
    /// <br/>
    /// Should run in O(K + logN) time or better, where K is the number of returned <typeparamref name="TFrame"/> instances and N is the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    /// <param name="lastKey">The higher bound of the query.</param>
    /// <returns>The <typeparamref name="TFrame"/> instances whose keys are in the given range.</returns>
    IEnumerable<TFrame> GetFramesBefore(TKey lastKey);

    /// <summary>
    /// Returns all <typeparamref name="TFrame"/> instances for key greater than or equal to <paramref name="firstKey"/>.
    /// <br/>
    /// Should run in O(K + logN) time or better, where K is the number of returned <typeparamref name="TFrame"/> instances and N is the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    /// <param name="firstKey">The lower bound of the query.</param>
    /// <returns>The <typeparamref name="TFrame"/> instances whose keys are in the given range.</returns>
    IEnumerable<TFrame> GetFramesAfter(TKey firstKey);

    /// <summary>
    /// Appends a sequence of <typeparamref name="TFrame"/> instances to the end of the storage, only the ascending prefixing portion will be appended.
    /// <br/>
    /// Should run in O(K + logN) time or better, where K is the number of appended <paramref name="frames"/> and N is the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    /// <param name="frames">A sequence of <typeparamref name="TFrame"/> instances to be appended.</param>
    /// <returns>The number of <typeparamref name="TFrame"/> instances appended.</returns>
    long AppendFrames(params TFrame[] frames);

    /// <summary>
    /// Appends a sequence of <typeparamref name="TFrame"/> instances to the end of the storage, only the ascending prefixing portion will be appended.
    /// <br/>
    /// Should run in O(K + logN) time or better, where K is the number of appended <paramref name="frames"/> and N is the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    /// <param name="frames">A sequence of <typeparamref name="TFrame"/> instances to be appended.</param>
    /// <returns>The number of <typeparamref name="TFrame"/> instances appended.</returns>
    long AppendFrames(IEnumerable<TFrame> frames);

    /// <summary>
    /// Appends frames to the end of the storage while enforcing the ascending order by key. No data will be appended if the ordering rule is violated.
    /// <br/>
    /// Should run in O(K + logN) time or better, where K is the number of appended <paramref name="frames"/> and N is the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    /// <param name="frames">A sequence of <typeparamref name="TFrame"/> instances to be appended.</param>
    /// <returns>The number of <typeparamref name="TFrame"/> instances appended.</returns>
    long AppendFramesTx(params TFrame[] frames);

    /// <summary>
    /// Appends frames to the end of the storage while enforcing the ascending order by key. No data will be appended if the ordering rule is violated.
    /// <br/>
    /// Should run in O(K + logN) time or better, where K is the number of appended <paramref name="frames"/> and N is the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    /// <param name="frames">A sequence of <typeparamref name="TFrame"/> instances to be appended.</param>
    /// <returns>The number of <typeparamref name="TFrame"/> instances appended.</returns>
    long AppendFramesTx(IEnumerable<TFrame> frames);

    /// <summary>
    /// Deletes all <typeparamref name="TFrame"/> instances of key equal to any of the <paramref name="keys"/>.
    /// The <paramref name="keys"/> must be ordered or a <see cref="ArgumentException"/> will be thrown before deleting any data.
    /// <br/>
    /// Should run in O(N + KlogN) time or better, where K is the number of <paramref name="keys"/> and N is the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    /// <param name="keys">A ordered list of keys of which the <typeparamref name="TFrame"/> instances to be deleted.</param>
    /// <returns>The number of <typeparamref name="TFrame"/> instances deleted.</returns>
    long DeleteFrames(params TKey[] keys);

    /// <summary>
    /// Deletes all <typeparamref name="TFrame"/> instances of key equal to any of the <paramref name="keys"/>.
    /// The <paramref name="keys"/> must be ordered or a <see cref="ArgumentException"/> will be thrown before deleting any data.
    /// <br/>
    /// Should run in O(N + KlogN) time or better, where K is the number of <paramref name="keys"/> and N is the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    /// <param name="keys">A ordered list of keys of which the <typeparamref name="TFrame"/> instances to be deleted.</param>
    /// <returns>The number of <typeparamref name="TFrame"/> instances deleted.</returns>
    long DeleteFrames(IEnumerable<TKey> keys);

    /// <summary>
    /// Deletes all <typeparamref name="TFrame"/> instances in the given range [<paramref name="firstKey"/>, <paramref name="lastKey"/>].
    /// <br/>
    /// Should run in O(N) time or better, where N is the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    /// <param name="firstKey">The lower bound of the query.</param>
    /// <param name="lastKey">The higher bound of the query.</param>
    /// <returns>The number of <typeparamref name="TFrame"/> instances deleted.</returns>
    long DeleteFramesBetween(TKey firstKey, TKey lastKey);

    /// <summary>
    /// Deletes all <typeparamref name="TFrame"/> instances of key less than or equal to <paramref name="lastKey"/>.
    /// <br/>
    /// Should run in O(N) time or better, where N is the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    /// <param name="lastKey">The higher bound of the deletion.</param>
    /// <returns>The number of <typeparamref name="TFrame"/> instances deleted.</returns>
    long DeleteFramesBefore(TKey lastKey);

    /// <summary>
    /// Deletes all <typeparamref name="TFrame"/> instances of key greater than or equal to <paramref name="firstKey"/>.
    /// <br/>
    /// Should run in O(N) time or better, where N is the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    /// <param name="firstKey">The lower bound of the deletion.</param>
    /// <returns>The number of <typeparamref name="TFrame"/> instances deleted.</returns>
    long DeleteFramesAfter(TKey firstKey);

    /// <summary>
    /// Deletes all <typeparamref name="TFrame"/> instances from the storage.
    /// <br/>
    /// Should run in O(N) time or better, where N is the number of <typeparamref name="TFrame"/> instances in the storage.
    /// </summary>
    /// <returns>The number of <typeparamref name="TFrame"/> instances deleted.</returns>
    long DeleteAllFrames();
}
