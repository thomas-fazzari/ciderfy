namespace Ciderfy.Matching;

/// <summary>
/// Version markers extracted from track titles and provider metadata.
/// </summary>
[Flags]
internal enum MusicVersionTag
{
    None = 0,
    Live = 1 << 0,
    Remix = 1 << 1,
    Remaster = 1 << 2,
    Mono = 1 << 3,
    Stereo = 1 << 4,
    RadioEdit = 1 << 5,
    Acoustic = 1 << 6,
    Instrumental = 1 << 7,
    Karaoke = 1 << 8,
    Explicit = 1 << 9,
    Clean = 1 << 10,
    ReRecorded = 1 << 11,
}
