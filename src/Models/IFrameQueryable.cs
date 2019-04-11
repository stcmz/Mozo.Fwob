using System;
using System.Collections.Generic;
using System.Text;

namespace Fwob.Models
{
    public interface IFrameQueryable<TFrame, TKey>
        where TFrame : IFrame<TKey>
        where TKey : struct, IComparable<TKey>
    {
        /// <summary>
        /// Gets the number of all <see cref="TFrame"/> in the storage.
        /// </summary>
        long FrameCount { get; }

        /// <summary>
        /// Gets the first <see cref="TFrame"/> instance in the storage if exists, null if otherwise.
        /// </summary>
        TFrame FirstFrame { get; }

        /// <summary>
        /// Gets the last <see cref="TFrame"/> instance in the storage if exists, null if otherwise.
        /// </summary>
        TFrame LastFrame { get; }

        /// <summary>
        /// Returns a <see cref="TFrame"/> at the given index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        TFrame GetFrame(long index);

        /// <summary>
        /// Returns an <see cref="IEnumerable{TFrame}"/> for the given query key.
        /// </summary>
        /// <param name="key">The query key</param>
        /// <returns></returns>
        IEnumerable<TFrame> GetFrames(TKey key);

        /// <summary>
        /// Returns all <see cref="TFrame"/> instances for the given range [<paramref name="firstKey"/>, <paramref name="lastKey"/>].
        /// </summary>
        /// <param name="firstKey">The lower bound of the query</param>
        /// <param name="lastKey">The higher bound of the query</param>
        /// <returns></returns>
        IEnumerable<TFrame> GetFrames(TKey firstKey, TKey lastKey);

        /// <summary>
        /// Returns all <see cref="TFrame"/> instances for key greater than or equal to <paramref name="firstKey"/>.
        /// </summary>
        /// <param name="firstKey">The lower bound of the query</param>
        /// <returns></returns>
        IEnumerable<TFrame> GetFramesAfter(TKey firstKey);

        /// <summary>
        /// Returns all <see cref="TFrame"/> instances for key less than or equal to <paramref name="lastKey"/>.
        /// </summary>
        /// <param name="lastKey">The higher bound of the query</param>
        /// <returns></returns>
        IEnumerable<TFrame> GetFramesBefore(TKey lastKey);

        /// <summary>
        /// Append frames to the end of the file, only the ascending prefix frames will be appended.
        /// </summary>
        /// <param name="frames">The frames to be appended</param>
        /// <returns>The number of frames appended</returns>
        long AppendFrames(params TFrame[] frames);

        /// <summary>
        /// Append frames to the end of the file, only the ascending prefix frames will be appended.
        /// </summary>
        /// <param name="frames">Frames to be appended</param>
        /// <returns>The number of frames appended</returns>
        long AppendFrames(IEnumerable<TFrame> frames);

        /// <summary>
        /// Append frames to the end of the file while enforcing the ascending order by key. No data will be appended if the ordering rule is violated.
        /// </summary>
        /// <param name="frames">The frames to be appended</param>
        /// <returns>The number of frames appended</returns>
        long AppendFramesTx(params TFrame[] frames);

        /// <summary>
        /// Append frames to the end of the file while enforcing the ascending order by key. No data will be appended if the ordering rule is violated.
        /// </summary>
        /// <param name="frames">Frames to be appended</param>
        /// <returns>The number of frames appended</returns>
        long AppendFramesTx(IEnumerable<TFrame> frames);

        /// <summary>
        /// Deletes all <see cref="TFrame"/> instances from the storage.
        /// </summary>
        void ClearFrames();
    }
}
