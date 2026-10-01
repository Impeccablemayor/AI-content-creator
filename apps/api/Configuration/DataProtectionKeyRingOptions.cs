namespace Api.Configuration;

public sealed class DataProtectionKeyRingOptions
{
    public const string SectionName = "DataProtection";

    /// <summary>
    /// Directory that holds the Data Protection key ring. Relative paths are
    /// resolved against the application content root.
    /// </summary>
    public string KeyRingPath { get; set; } = string.Empty;

    /// <summary>
    /// Discriminator that isolates this application's key ring from other
    /// applications sharing the same key storage.
    /// </summary>
    public string ApplicationName { get; set; } = "mayorx-content-engine";
}
