using Wabbajack.Paths;

namespace Wabbajack;

/// <summary>
/// Runtime settings that can be modified during app execution.
/// These are not persisted and reset to defaults on app restart.
/// </summary>
public class RuntimeSettings
{
    /// <summary>
    /// When true, manual download links will open in the external browser
    /// with the Floating Companion Window instead of the embedded browser.
    /// </summary>
    public bool UseExternalBrowserForManualDownloads { get; set; } = false;
    
    /// <summary>
    /// The current download location path for the modlist installation.
    /// Used to inform users where to copy downloaded files when using external browser.
    /// </summary>
    public AbsolutePath DownloadLocation { get; set; } = default;
}
