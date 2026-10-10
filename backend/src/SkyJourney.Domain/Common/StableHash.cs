using System.Text;

namespace SkyJourney.Domain.Common;

/// <summary>
/// FNV-1a 32-bit hash. Unlike <see cref="string.GetHashCode()"/>, the result is identical across
/// processes and machines, which deterministic demo data generation relies on.
/// </summary>
public static class StableHash
{
    public static uint Fnv1A(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= prime;
        }

        return hash;
    }
}
