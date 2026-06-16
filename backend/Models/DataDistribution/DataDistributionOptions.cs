namespace TapeReplay.Api.Models.DataDistribution;

/// <summary>
/// Configuration for market data CDN publish and subscribe.
/// </summary>
public sealed class DataDistributionOptions
{
    public const string SectionName = "DataDistribution";

    /// <summary>Publisher, Subscriber, or Both.</summary>
    public DataDistributionRole Role { get; set; } = DataDistributionRole.Both;

    /// <summary>URL of manifest.json on the data CDN (separate from app-update manifest).</summary>
    public string ManifestUrl { get; set; } = string.Empty;

    /// <summary>Base URL for partition downloads.</summary>
    public string CdnBaseUrl { get; set; } = string.Empty;

    /// <summary>Local directory written by publish (user syncs to CDN externally).</summary>
    public string PublishDirectory { get; set; } = "publish/data";

    /// <summary>Run subscriber sync when the API starts.</summary>
    public bool SyncOnLaunch { get; set; } = true;

    /// <summary>When null, derived from Role (off for Subscriber, on for Publisher/Both).</summary>
    public bool? ScraperEnabled { get; set; }

    /// <summary>Include a bootstrap tar archive in publish output.</summary>
    public bool IncludeBootstrapArchive { get; set; } = true;

    /// <summary>Dataset version string stamped into manifest.json.</summary>
    public string DatasetVersion { get; set; } = "1";

    /// <summary>Schema version for parquet layout evolution.</summary>
    public string SchemaVersion { get; set; } = "1";

    public bool IsScraperEnabled() => ScraperEnabled ?? Role is DataDistributionRole.Publisher or DataDistributionRole.Both;

    public bool CanPublish() => Role is DataDistributionRole.Publisher or DataDistributionRole.Both;

    public bool CanSubscribe() => Role is DataDistributionRole.Subscriber or DataDistributionRole.Both;
}
