using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.Installer;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Paths;
using System.Reactive.Linq;

namespace Wabbajack;

public class MO2InstallerVM : ViewModel, ISubInstallerVM
{
    public InstallationVM Parent { get; }
    private readonly RuntimeSettings _runtimeSettings;

    [Reactive]
    public ValidationResult CanInstall { get; set; }

    [Reactive]
    public IInstaller ActiveInstallation { get; private set; }

    [Reactive]
    public Mo2ModlistInstallationSettings CurrentSettings { get; set; }

    public FilePickerVM Location { get; }

    public FilePickerVM DownloadLocation { get; }
    
    public FilePickerVM GameFolderLocation { get; }

    public bool SupportsAfterInstallNavigation => true;

    [Reactive]
    public bool AutomaticallyOverwrite { get; set; }
    
    /// <summary>
    /// When enabled, mod links will open in the user's default external browser
    /// with a floating companion window for navigation instead of the embedded browser.
    /// </summary>
    [Reactive]
    public bool UseExternalBrowserWithCompanion { get; set; }

    public int ConfigVisualVerticalOffset => 25;

    public MO2InstallerVM(InstallationVM installerVM, RuntimeSettings runtimeSettings)
    {
        Parent = installerVM;
        _runtimeSettings = runtimeSettings;

        Location = new FilePickerVM()
        {
            ExistCheckOption = FilePickerVM.CheckOptions.Off,
            PathType = FilePickerVM.PathTypeOptions.Folder,
            PromptTitle = "Select a location to install Mod Organizer 2 to.",
        };
        Location.WhenAnyValue(t => t.TargetPath)
            .Subscribe(newPath =>
            {
                if (newPath != default && DownloadLocation!.TargetPath == AbsolutePath.Empty)
                {
                    DownloadLocation.TargetPath = newPath.Combine("downloads");
                }
            }).DisposeWith(CompositeDisposable);

        DownloadLocation = new FilePickerVM()
        {
            ExistCheckOption = FilePickerVM.CheckOptions.Off,
            PathType = FilePickerVM.PathTypeOptions.Folder,
            PromptTitle = "Select a location to store downloaded mod archives.",
        };
        
        GameFolderLocation = new FilePickerVM()
        {
            ExistCheckOption = FilePickerVM.CheckOptions.IfPathNotEmpty,
            PathType = FilePickerVM.PathTypeOptions.Folder,
            PromptTitle = "Select the game folder (optional - only needed if game was moved/copied).",
        };
        
        // Sync the checkbox with the runtime settings
        this.WhenAnyValue(x => x.UseExternalBrowserWithCompanion)
            .Subscribe(value => _runtimeSettings.UseExternalBrowserForManualDownloads = value)
            .DisposeWith(CompositeDisposable);
        
        // Sync the download location with runtime settings
        DownloadLocation.WhenAnyValue(x => x.TargetPath)
            .Subscribe(value => _runtimeSettings.DownloadLocation = value)
            .DisposeWith(CompositeDisposable);
    }

    public void Unload()
    {
        SaveSettings(this.CurrentSettings);
    }

    private void SaveSettings(Mo2ModlistInstallationSettings settings)
    {
        //Parent.MWVM.Settings.Installer.LastInstalledListLocation = Parent.ModListLocation.TargetPath;
        if (settings == null) return;
        settings.InstallationLocation = Location.TargetPath;
        settings.DownloadLocation = DownloadLocation.TargetPath;
        settings.AutomaticallyOverrideExistingInstall = AutomaticallyOverwrite;
    }

    public void AfterInstallNavigation()
    {
        Process.Start("explorer.exe", Location.TargetPath.ToString());
    }

    public async Task<bool> Install()
    {
        /*
        using (var installer = new MO2Installer(
            archive: Parent.ModListLocation.TargetPath,
            modList: Parent.ModList.SourceModList,
            outputFolder: Location.TargetPath,
            downloadFolder: DownloadLocation.TargetPath,
            parameters: SystemParametersConstructor.Create()))
        {
            installer.Metadata = Parent.ModList.SourceModListMetadata;
            installer.UseCompression = Parent.MWVM.Settings.Filters.UseCompression;
            Parent.MWVM.Settings.Performance.SetProcessorSettings(installer);

            return await Task.Run(async () =>
            {
                try
                {
                    var workTask = installer.Begin();
                    ActiveInstallation = installer;
                    return await workTask;
                }
                finally
                {
                    ActiveInstallation = null;
                }
            });
        }
        */
        return true;
    }

    public IUserIntervention InterventionConverter(IUserIntervention intervention)
    {
        switch (intervention)
        {
            case ConfirmUpdateOfExistingInstall confirm:
                return new ConfirmUpdateOfExistingInstallVM(this, confirm);
            default:
                return intervention;
        }
    }
}
