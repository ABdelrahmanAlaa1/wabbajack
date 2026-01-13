using System;
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
    
    /// <summary>
    /// The expected total count of manual downloads for the current installation.
    /// Set before installation starts by counting archives with Manual state.
    /// This allows the Floating Companion Window to show accurate "X of Y" progress from the start.
    /// </summary>
    public int ExpectedManualDownloadCount { get; set; } = 0;
    
    /// <summary>
    /// Action to cancel the current installation. Set by InstallationVM when installation begins.
    /// </summary>
    public Action? CancelInstallation { get; set; }
}
