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
using Wabbajack.Services;
using Wabbajack.UserIntervention;
using Wabbajack.Views.ModBrowserCompanion;

namespace Wabbajack.Interventions;

public class UserInterventionHandler : IUserInterventionHandler
{
    private readonly ILogger<UserInterventionHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly RuntimeSettings _runtimeSettings;
    private readonly ExternalBrowserDownloadManager _externalBrowserManager;

    public UserInterventionHandler(ILogger<UserInterventionHandler> logger, IServiceProvider serviceProvider, 
        RuntimeSettings runtimeSettings, ExternalBrowserDownloadManager externalBrowserManager)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _runtimeSettings = runtimeSettings;
        _externalBrowserManager = externalBrowserManager;
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
                    // The manager accumulates all downloads and shows a single window
                    _externalBrowserManager.AddDownload(md);
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
}