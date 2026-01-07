using System.Collections.Generic;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Services.OSIntegrated;

public class StubbedGameLocator : IGameLocator
{
    private readonly TemporaryPath _location;
    private readonly TemporaryFileManager _manager;
    private readonly Dictionary<Game, AbsolutePath> _overrides = new();

    public StubbedGameLocator(TemporaryFileManager manager)
    {
        _manager = manager;
        _location = manager.CreateFolder();
    }

    public AbsolutePath GameLocation(Game game)
    {
        if (_overrides.TryGetValue(game, out var path))
            return path;
        return _location.Path;
    }

    public bool IsInstalled(Game game)
    {
        return true;
    }

    public bool TryFindLocation(Game game, out AbsolutePath path)
    {
        if (_overrides.TryGetValue(game, out path))
            return true;
        path = _location.Path;
        return true;
    }
    
    public void SetGameLocationOverride(Game game, AbsolutePath path)
    {
        _overrides[game] = path;
    }
    
    public void ClearGameLocationOverride(Game game)
    {
        _overrides.Remove(game);
    }
}