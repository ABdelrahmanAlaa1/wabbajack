using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.Logging;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Paths;
using Wabbajack.Views.ModBrowserCompanion;

namespace Wabbajack.Services;

/// <summary>
/// Manages external browser downloads with a floating companion window.
/// Collects all manual download interventions and processes them together.
/// </summary>
public class ExternalBrowserDownloadManager
{
    private readonly ILogger<ExternalBrowserDownloadManager> _logger;
    private readonly RuntimeSettings _runtimeSettings;
    
    private readonly List<ManualDownload> _pendingDownloads = new();
    private readonly List<ModLink> _modLinks = new();
    private FloatingCompanionWindow? _companionWindow;
    private int _currentIndex;
    private bool _isProcessing;
    private bool _isCancelled;
    
    // Persistent counter that tracks total mods processed across all sessions
    private int _totalProcessedCount;

    public ExternalBrowserDownloadManager(ILogger<ExternalBrowserDownloadManager> logger, RuntimeSettings runtimeSettings)
    {
        _logger = logger;
        _runtimeSettings = runtimeSettings;
    }

    /// <summary>
    /// Gets the expected total count of manual downloads from RuntimeSettings.
    /// Falls back to current mod list count if not set.
    /// </summary>
    private int GetExpectedTotalCount()
    {
        var expected = _runtimeSettings.ExpectedManualDownloadCount;
        return expected > 0 ? expected : _modLinks.Count;
    }

    /// <summary>
    /// Adds a manual download to be processed.
    /// Each download creates a simple session - user clicks Next/Skip to finish and move to next mod.
    /// </summary>
    public void AddDownload(ManualDownload download)
    {
        // If installation was cancelled, immediately reject the download
        if (_isCancelled)
        {
            _logger.LogInformation("Installation cancelled - rejecting new download: {Name}", download.Archive.Name);
            download.Finish(null);
            return;
        }
        
        var manual = download.Archive.State as Manual;
        if (manual == null)
        {
            _logger.LogError("ManualDownload intervention has no Manual state");
            download.Finish(null);
            return;
        }

        // Increment the persistent counter
        _totalProcessedCount++;
        
        var expectedTotal = GetExpectedTotalCount();
        _logger.LogInformation("Manual download {Index}/{Total}: {Name}", 
            _totalProcessedCount, expectedTotal, download.Archive.Name);
        
        // Store this single download for the session
        _pendingDownloads.Clear();
        _modLinks.Clear();
        _pendingDownloads.Add(download);
        _modLinks.Add(new ModLink(download.Archive.Name, manual.Url.ToString()));
        _currentIndex = 0;
        
        // Start/update the session
        StartOrUpdateSession();
    }

    private void StartOrUpdateSession()
    {
        _isProcessing = true;
        _isCancelled = false;

        Application.Current.Dispatcher.Invoke(() =>
        {
            // Open the mod link in external browser
            OpenModLinkAtIndex(0);

            var expectedTotal = GetExpectedTotalCount();
            _logger.LogInformation("Browser session for mod {Index}/{Total}", _totalProcessedCount, expectedTotal);

            // Close existing window if any
            _companionWindow?.Close();
            
            // Show the floating companion window - display uses _totalProcessedCount for "Mod X of Y"
            _companionWindow = new FloatingCompanionWindow(
                _modLinks, 
                0, // Always index 0 since we only have 1 mod per session
                expectedTotal,
                OnNextClicked,
                OnSkipClicked,
                OnCancelClicked,
                OnFinishAndCopyClicked,
                _totalProcessedCount); // Pass the persistent counter for display
            _companionWindow.Show();
        });
    }

    private void OpenModLinkAtIndex(int index)
    {
        if (index < 0 || index >= _modLinks.Count) return;

        var link = _modLinks[index];
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = link.Url,
                UseShellExecute = true
            });
            _logger.LogInformation("Opened mod link in external browser: {Name} - {Url}", link.Name, link.Url);
            
            // Update counter display immediately after link is sent
            _companionWindow?.UpdateModList(_modLinks, _currentIndex, GetExpectedTotalCount());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open URL in external browser: {Url}", link.Url);
        }
    }

    private void OnNextClicked()
    {
        // User clicked Next - finish this session and allow next mod to arrive
        _logger.LogInformation("OnNextClicked: Finished mod {Index}/{Total}: {Name}", 
            _totalProcessedCount, GetExpectedTotalCount(), _modLinks[0].Name);
        FinishCurrentSession();
    }

    private void OnSkipClicked()
    {
        // Log this mod as skipped
        _logger.LogWarning("User skipped mod {Index}/{Total}: {Name}", 
            _totalProcessedCount, GetExpectedTotalCount(), _modLinks[0].Name);
        FinishCurrentSession();
    }

    private void OnCancelClicked()
    {
        // User clicked Cancel - show confirmation dialog before cancelling the installation
        var result = MessageBox.Show(
            "Are you sure you want to cancel the installation?\n\nThis will stop all downloads and the installation process.",
            "Cancel Installation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        
        if (result == MessageBoxResult.Yes)
        {
            _logger.LogInformation("User confirmed cancellation of installation at mod {Index} of {Total}", 
                _currentIndex + 1, _modLinks.Count);
            
            // Mark as cancelled to reject any future downloads
            _isCancelled = true;
            
            // Cancel the installation via RuntimeSettings action
            _runtimeSettings.CancelInstallation?.Invoke();
            
            // Finish without file copy prompt
            FinishWithoutCopy();
        }
    }

    private void OnFinishAndCopyClicked()
    {
        // User clicked "Finish & Copy" on the last mod - show file copy prompt
        FinishWithCopyPrompt();
    }

    /// <summary>
    /// Finishes the current single-mod session, allowing the next mod to be processed.
    /// </summary>
    private void FinishCurrentSession()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _companionWindow?.Close();
            _companionWindow = null;

            // Finish the current download (with null since user downloads manually)
            foreach (var download in _pendingDownloads)
            {
                download.Finish(null);
            }

            // Clear session state but keep _totalProcessedCount and _isProcessing
            _pendingDownloads.Clear();
            _modLinks.Clear();
            _currentIndex = 0;
            // Note: _isProcessing stays true to indicate we're in a download workflow
            // Note: _totalProcessedCount is preserved for the next mod's display
        });
    }

    private void FinishWithoutCopy()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _companionWindow?.Close();
            _companionWindow = null;

            _logger.LogInformation("External browser download session ended without file copy for {Count} mods", _pendingDownloads.Count);

            // Finish all pending downloads
            foreach (var download in _pendingDownloads)
            {
                download.Finish(null);
            }

            // Reset state
            _pendingDownloads.Clear();
            _modLinks.Clear();
            _currentIndex = 0;
            _isProcessing = false;
        });
    }

    private void FinishWithCopyPrompt()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _companionWindow?.Close();
            _companionWindow = null;

            // Show a SINGLE prompt to copy ALL downloaded files
            var downloadPath = _runtimeSettings.DownloadLocation != default
                ? _runtimeSettings.DownloadLocation.ToString()
                : "the modlist downloads folder";

            var result = MessageBox.Show(
                $"Please copy ALL downloaded files ({_modLinks.Count} mod(s)) to:\n\n{downloadPath}\n\nClick OK once you have copied all files, or Cancel to continue without copying.",
                "Copy Downloaded Files",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.OK)
            {
                _logger.LogInformation("User confirmed file copy for {Count} manual downloads", _pendingDownloads.Count);
            }
            else
            {
                _logger.LogWarning("User skipped file copy for {Count} manual downloads", _pendingDownloads.Count);
            }

            // Finish all pending downloads
            foreach (var download in _pendingDownloads)
            {
                download.Finish(null);
            }

            // Reset state
            _pendingDownloads.Clear();
            _modLinks.Clear();
            _currentIndex = 0;
            _isProcessing = false;
        });
    }

    /// <summary>
    /// Resets the manager state. Call this when starting a new installation.
    /// </summary>
    public void Reset()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _companionWindow?.Close();
            _companionWindow = null;
        });

        foreach (var download in _pendingDownloads)
        {
            download.Finish(null);
        }

        _pendingDownloads.Clear();
        _modLinks.Clear();
        _currentIndex = 0;
        _totalProcessedCount = 0; // Reset the persistent counter for new installation
        _isProcessing = false;
        _isCancelled = false;
    }
}
