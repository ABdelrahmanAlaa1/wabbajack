using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Messages;
using Wabbajack.UserIntervention;
using Wabbajack.Views.ModBrowserCompanion;

namespace Wabbajack.Interventions;

public class UserInterventionHandler : IUserInterventionHandler
{
    private readonly ILogger<UserInterventionHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly RuntimeSettings _runtimeSettings;

    public UserInterventionHandler(ILogger<UserInterventionHandler> logger, IServiceProvider serviceProvider, RuntimeSettings runtimeSettings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _runtimeSettings = runtimeSettings;
    }
    public void Raise(IUserIntervention intervention)
    {
        switch (intervention)
        {
            // Recast these or they won't be properly handled by the message bus
            case ManualDownload md:
            {
                if (_runtimeSettings.UseExternalBrowserForManualDownloads)
                {
                    // Open in external browser with floating companion window
                    HandleManualDownloadExternal(md);
                }
                else
                {
                    // Use embedded browser (default behavior)
                    var provider = _serviceProvider.GetRequiredService<ManualDownloadHandler>();
                    provider.Intervention = md;
                    MessageBus.Current.SendMessage(new ShowBrowserWindow(provider));
                }
                break;
            }
            case ManualBlobDownload bd:
            {
                var provider = _serviceProvider.GetRequiredService<ManualBlobDownloadHandler>();
                provider.Intervention = bd;
                MessageBus.Current.SendMessage(new ShowBrowserWindow(provider));
                break;
            }
            default:
                _logger.LogError("No handler for user intervention: {Type}", intervention);
                break;

        }

    }
    
    private void HandleManualDownloadExternal(ManualDownload md)
    {
        var manual = md.Archive.State as Manual;
        if (manual == null)
        {
            _logger.LogError("ManualDownload intervention has no Manual state");
            md.Finish(null);
            return;
        }
        
        var modLink = new ModLink(md.Archive.Name, manual.Url.ToString());
        var modLinks = new List<ModLink> { modLink };
        
        // Open external browser
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = manual.Url.ToString(),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open URL in external browser: {Url}", manual.Url);
        }
        
        // Show floating companion window
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            FloatingCompanionWindow.Show(modLinks, () =>
            {
                // When user clicks Return, show a prompt to copy files to download directory
                var downloadPath = _runtimeSettings.DownloadLocation != default 
                    ? _runtimeSettings.DownloadLocation.ToString() 
                    : "the modlist downloads folder";
                
                var result = MessageBox.Show(
                    $"Please copy the downloaded file(s) to:\n\n{downloadPath}\n\nClick OK once you have copied the file(s), or Cancel to skip this download.",
                    "Copy Downloaded Files",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information);
                
                if (result == MessageBoxResult.OK)
                {
                    _logger.LogInformation("User confirmed file copy for manual download: {Name}", md.Archive.Name);
                }
                else
                {
                    _logger.LogWarning("User skipped file copy for manual download: {Name}", md.Archive.Name);
                }
                
                md.Finish(null);
            });
        });
    }
}