using System.Collections.Generic;

namespace AutoGameBench.Steam;

internal sealed class LibraryFolders<TKey, TValue> : Dictionary<TKey, TValue>
{
    public string ContentStatsId { get; init; }
}
