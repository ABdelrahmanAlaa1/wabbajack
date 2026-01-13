using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack;

/// <summary>
/// Provides validation for game folder paths to help users select the correct directory.
/// </summary>
public static class GameFolderValidation
{
    /// <summary>
    /// Validates the selected game folder path and returns a result indicating if it appears correct.
    /// </summary>
    public static GameFolderValidationResult ValidateGameFolder(AbsolutePath selectedPath, Game game)
    {
        if (selectedPath == default)
            return GameFolderValidationResult.Empty();

        var gameMetaData = game.MetaData();
        var selectedFolderName = selectedPath.FileName.ToString();
        
        // Check if required files exist in the selected folder
        var hasRequiredFiles = gameMetaData.RequiredFiles
            .Any(rf => selectedPath.Combine(rf).FileExists());
        
        // If required files exist, the path is likely correct
        if (hasRequiredFiles)
            return GameFolderValidationResult.Valid();

        // Check if the path is too deep (e.g., user selected a subfolder like bin, Data, etc.)
        var commonSubfolders = new[] { "bin", "data", "mods", "scripts", "textures", "meshes", "sound", "video", "strings" };
        if (commonSubfolders.Any(sf => selectedFolderName.Equals(sf, StringComparison.OrdinalIgnoreCase)))
        {
            return GameFolderValidationResult.TooDeep(
                $"The selected folder '{selectedFolderName}' appears to be a subfolder within the game directory. " +
                $"Please select the main game folder (e.g., the folder containing {gameMetaData.MainExecutable?.ToString() ?? "the game executable"}).");
        }

        // Check if selected folder name doesn't match expected game folder patterns
        var expectedPatterns = GetExpectedFolderPatterns(gameMetaData);
        var matchesExpected = expectedPatterns.Any(pattern => 
            selectedFolderName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        
        if (!matchesExpected && expectedPatterns.Any())
        {
            return GameFolderValidationResult.PossibleMismatch(
                $"The selected folder '{selectedFolderName}' doesn't appear to match the expected game folder for {gameMetaData.HumanFriendlyGameName}. " +
                $"Expected a folder name containing: {string.Join(", ", expectedPatterns)}. " +
                $"If you're sure this is correct, you can proceed anyway.");
        }

        // Check if the path seems incomplete (very shallow path)
        if (selectedPath.Depth <= 2)
        {
            return GameFolderValidationResult.TooShallow(
                $"The selected path '{selectedPath}' appears to be at the root level. " +
                $"Please navigate to the actual game installation folder.");
        }

        // Path looks OK but no required files found - could be moved/copied game
        return GameFolderValidationResult.MissingFiles(
            $"Could not find expected game files in the selected folder. " +
            $"Expected to find: {gameMetaData.MainExecutable?.ToString() ?? "game executable"}. " +
            $"If this is a moved/copied game installation, you may proceed anyway.");
    }

    private static string[] GetExpectedFolderPatterns(GameMetaData metaData)
    {
        return metaData.Game switch
        {
            Game.Cyberpunk2077 => new[] { "Cyberpunk", "2077" },
            Game.SkyrimSpecialEdition => new[] { "Skyrim Special Edition", "SkyrimSE" },
            Game.Skyrim => new[] { "Skyrim" },
            Game.Fallout4 => new[] { "Fallout 4", "Fallout4" },
            Game.FalloutNewVegas => new[] { "Fallout New Vegas", "FalloutNV" },
            Game.Fallout3 => new[] { "Fallout 3", "Fallout3" },
            Game.Oblivion => new[] { "Oblivion" },
            Game.Morrowind => new[] { "Morrowind" },
            Game.Witcher3 => new[] { "Witcher 3", "Witcher3" },
            Game.BaldursGate3 => new[] { "Baldur's Gate 3", "BaldursGate3" },
            Game.Starfield => new[] { "Starfield" },
            Game.DragonAgeOrigins => new[] { "Dragon Age", "DragonAge" },
            _ => new[] { metaData.MO2Name ?? metaData.NexusName ?? "" }
        };
    }

    /// <summary>
    /// Shows a validation warning dialog and returns the user's choice.
    /// </summary>
    public static Task<GameFolderValidationChoice> ShowValidationDialog(GameFolderValidationResult result)
    {
        var tcs = new TaskCompletionSource<GameFolderValidationChoice>();
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            var dialogResult = MessageBox.Show(
                result.Message + "\n\nDo you want to continue with this selection?",
                "Game Folder Validation Warning",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            var choice = dialogResult switch
            {
                MessageBoxResult.Yes => GameFolderValidationChoice.IgnoreAndContinue,
                MessageBoxResult.No => GameFolderValidationChoice.ReselectPath,
                _ => GameFolderValidationChoice.Cancel
            };
            
            tcs.SetResult(choice);
        });
        
        return tcs.Task;
    }
}

public enum GameFolderValidationChoice
{
    ReselectPath,
    IgnoreAndContinue,
    Cancel
}

public class GameFolderValidationResult
{
    public bool IsValid { get; private set; }
    public bool RequiresUserConfirmation { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public GameFolderValidationType Type { get; private set; }

    public static GameFolderValidationResult Valid() => new()
    {
        IsValid = true,
        RequiresUserConfirmation = false,
        Type = GameFolderValidationType.Valid
    };

    public static GameFolderValidationResult Empty() => new()
    {
        IsValid = true,
        RequiresUserConfirmation = false,
        Type = GameFolderValidationType.Empty
    };

    public static GameFolderValidationResult TooDeep(string message) => new()
    {
        IsValid = false,
        RequiresUserConfirmation = true,
        Message = message,
        Type = GameFolderValidationType.TooDeep
    };

    public static GameFolderValidationResult TooShallow(string message) => new()
    {
        IsValid = false,
        RequiresUserConfirmation = true,
        Message = message,
        Type = GameFolderValidationType.TooShallow
    };

    public static GameFolderValidationResult PossibleMismatch(string message) => new()
    {
        IsValid = false,
        RequiresUserConfirmation = true,
        Message = message,
        Type = GameFolderValidationType.PossibleMismatch
    };

    public static GameFolderValidationResult MissingFiles(string message) => new()
    {
        IsValid = false,
        RequiresUserConfirmation = true,
        Message = message,
        Type = GameFolderValidationType.MissingFiles
    };
}

public enum GameFolderValidationType
{
    Valid,
    Empty,
    TooDeep,
    TooShallow,
    PossibleMismatch,
    MissingFiles
}
