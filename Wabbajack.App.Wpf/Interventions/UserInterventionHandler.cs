using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                // When user clicks Return, finish the intervention
                // Note: User must manually download the file to the downloads folder
                // The download won't be automatically captured like with the embedded browser
                _logger.LogInformation("User returned from external browser for manual download: {Name}", md.Archive.Name);
                md.Finish(null);
            });
        });
    }
}