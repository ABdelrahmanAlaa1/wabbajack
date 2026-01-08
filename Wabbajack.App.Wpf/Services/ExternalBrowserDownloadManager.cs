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

    public ExternalBrowserDownloadManager(ILogger<ExternalBrowserDownloadManager> logger, RuntimeSettings runtimeSettings)
    {
        _logger = logger;
        _runtimeSettings = runtimeSettings;
    }

    /// <summary>
    /// Adds a manual download to be processed.
    /// If this is the first download, it opens the floating companion window.
    /// </summary>
    public void AddDownload(ManualDownload download)
    {
        var manual = download.Archive.State as Manual;
        if (manual == null)
        {
            _logger.LogError("ManualDownload intervention has no Manual state");
            download.Finish(null);
            return;
        }

        _pendingDownloads.Add(download);
        _modLinks.Add(new ModLink(download.Archive.Name, manual.Url.ToString()));

        // If we're not already processing, start the companion window
        if (!_isProcessing)
        {
            StartProcessing();
        }
        else
        {
            // Update the existing window to show new total
            Application.Current.Dispatcher.Invoke(() =>
            {
                _companionWindow?.UpdateModList(_modLinks, _currentIndex);
            });
        }
    }

    private void StartProcessing()
    {
        _isProcessing = true;
        _currentIndex = 0;

        Application.Current.Dispatcher.Invoke(() =>
        {
            // Open the first mod link in external browser
            OpenModLinkAtIndex(0);

            // Show the floating companion window
            _companionWindow = new FloatingCompanionWindow(
                _modLinks, 
                _currentIndex,
                OnNextClicked,
                OnSkipClicked,
                OnFinishClicked);
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

    private void OnNextClicked()
    {
        if (_currentIndex < _modLinks.Count - 1)
        {
            _currentIndex++;
            OpenModLinkAtIndex(_currentIndex);
            _companionWindow?.UpdateCurrentIndex(_currentIndex);
        }
    }

    private void OnSkipClicked()
    {
        // Skip current mod without copying, move to next
        _logger.LogWarning("User skipped mod: {Name}", _modLinks[_currentIndex].Name);
        
        if (_currentIndex < _modLinks.Count - 1)
        {
            _currentIndex++;
            OpenModLinkAtIndex(_currentIndex);
            _companionWindow?.UpdateCurrentIndex(_currentIndex);
        }
        else
        {
            // This was the last mod, show finish prompt
            FinishAllDownloads();
        }
    }

    private void OnFinishClicked()
    {
        FinishAllDownloads();
    }

    private void FinishAllDownloads()
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
    }
}
