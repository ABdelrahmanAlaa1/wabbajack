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
    /// If this is the first download, it opens the floating companion window.
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

        _pendingDownloads.Add(download);
        _modLinks.Add(new ModLink(download.Archive.Name, manual.Url.ToString()));

        var expectedTotal = GetExpectedTotalCount();
        _logger.LogInformation("Added manual download {Index}/{Total}: {Name}", 
            _modLinks.Count, expectedTotal, download.Archive.Name);
        
        // If we're not already processing, start the companion window
        if (!_isProcessing)
        {
            StartProcessing();
        }
        else
        {
            // Update the existing window with current list and expected total
            Application.Current.Dispatcher.Invoke(() =>
            {
                _companionWindow?.UpdateModList(_modLinks, _currentIndex, expectedTotal);
            });
        }
    }

    private void StartProcessing()
    {
        _isProcessing = true;
        _isCancelled = false;
        _currentIndex = 0;

        Application.Current.Dispatcher.Invoke(() =>
        {
            // Open the first mod link in external browser
            OpenModLinkAtIndex(0);

            var expectedTotal = GetExpectedTotalCount();
            _logger.LogInformation("Starting external browser download session. Expected total: {ExpectedTotal}, Current count: {CurrentCount}", 
                expectedTotal, _modLinks.Count);

            // Show the floating companion window with separate callbacks for Cancel and Finish & Copy
            _companionWindow = new FloatingCompanionWindow(
                _modLinks, 
                _currentIndex,
                expectedTotal,
                OnNextClicked,
                OnSkipClicked,
                OnCancelClicked,
                OnFinishAndCopyClicked);
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open URL in external browser: {Url}", link.Url);
        }
    }

    /// <summary>
    /// Advances to the next mod in the list. Used by both Next and Skip buttons.
    /// </summary>
    /// <param name="isSkip">If true, logs a warning that the mod was skipped</param>
    private void AdvanceToNextMod(bool isSkip)
    {
        // Only difference: Skip logs a warning for tracking skipped mods
        if (isSkip)
        {
            _logger.LogWarning("User skipped mod: {Name}", _modLinks[_currentIndex].Name);
        }
        
        // Same logic for both Next and Skip
        if (_currentIndex < _modLinks.Count - 1)
        {
            _currentIndex++;
            OpenModLinkAtIndex(_currentIndex);
            _companionWindow?.UpdateModList(_modLinks, _currentIndex, GetExpectedTotalCount());
        }
        else
        {
            // This was the last mod - finish without file copy prompt
            FinishWithoutCopy();
        }
    }

    private void OnNextClicked()
    {
        AdvanceToNextMod(isSkip: false);
    }

    private void OnSkipClicked()
    {
        AdvanceToNextMod(isSkip: true);
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
        _isProcessing = false;
        _isCancelled = false;
    }
}
