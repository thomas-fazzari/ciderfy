namespace Ciderfy.Matching;

internal static class MatchingWeights
{
    internal const double Title = 0.6;
    internal const double Artist = 0.4;
}

internal enum MatchMethod
{
    Isrc = 0,
    Text = 1,
}
