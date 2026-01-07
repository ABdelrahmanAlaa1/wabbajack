using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.Downloaders.GameFile;

public interface IGameLocator
{
    public AbsolutePath GameLocation(Game game);
    public bool IsInstalled(Game game);
    public bool TryFindLocation(Game game, out AbsolutePath path);
    
    /// <summary>
    /// Sets a manual override for a game's location. This allows users to specify
    /// a custom game folder path when the game is copied or moved to a different location.
    /// </summary>
    /// <param name="game">The game to override the location for</param>
    /// <param name="path">The custom path to the game folder</param>
    public void SetGameLocationOverride(Game game, AbsolutePath path);
    
    /// <summary>
    /// Clears any manual override for a game's location.
    /// </summary>
    /// <param name="game">The game to clear the override for</param>
    public void ClearGameLocationOverride(Game game);
}