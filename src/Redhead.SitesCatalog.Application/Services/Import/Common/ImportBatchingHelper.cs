namespace Redhead.SitesCatalog.Application.Services.Import.Common;

internal static class ImportBatchingHelper
{
    public static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
        {
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }
}
