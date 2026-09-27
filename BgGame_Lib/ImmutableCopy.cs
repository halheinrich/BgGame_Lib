namespace BgGame_Lib;

using System.Collections.Immutable;

/// <summary>
/// The one way this library's records take in a collection: an immutable
/// copy, made where the record is built. The record then hands out nothing
/// its caller can change afterwards, and nothing a cast can write — an
/// <see cref="ImmutableArray{T}"/> seen as an <see cref="IReadOnlyList{T}"/>
/// is still immutable.
/// </summary>
internal static class ImmutableCopy
{
    /// <summary>
    /// An immutable copy of <paramref name="source"/> in its order, boxed once
    /// as the list the record holds and hands out; an immutable array is taken
    /// as it is, since it cannot change.
    /// </summary>
    /// <param name="source">The collection a caller passed.</param>
    /// <param name="paramName">The member the refusal names.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    internal static IReadOnlyList<T> Of<T>(IEnumerable<T>? source, string paramName) =>
        source is null
            ? throw new ArgumentNullException(paramName)
            : ImmutableArray.CreateRange(source);
}
