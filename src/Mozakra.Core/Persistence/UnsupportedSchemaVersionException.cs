namespace Mozakra.Core.Persistence;

/// <summary>The data file was written by a newer Mozakra version than this one.</summary>
public sealed class UnsupportedSchemaVersionException : Exception
{
    public UnsupportedSchemaVersionException(int foundVersion, int supportedVersion)
        : base($"The data file uses schema version {foundVersion}, but this version of Mozakra " +
               $"supports up to {supportedVersion}. Please update Mozakra.")
    {
        FoundVersion = foundVersion;
        SupportedVersion = supportedVersion;
    }

    public int FoundVersion { get; }
    public int SupportedVersion { get; }
}
