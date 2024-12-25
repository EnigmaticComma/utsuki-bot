namespace UtsukiBot.Extensions;

public static class EnumeratorExtensions
{
    public static bool GetIfInRange<T>(this ICollection<T> collection, int index, out T value)
    {
        if (index < 0 || index >= collection.Count)
        {
            value = default;
            return false;
        }

        value = collection.ElementAt(index);
        return true;
    }
}