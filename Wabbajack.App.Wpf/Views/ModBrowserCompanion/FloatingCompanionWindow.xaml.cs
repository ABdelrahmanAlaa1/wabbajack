using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace Wabbajack.Views.ModBrowserCompanion;

/// <summary>
/// Floating companion window that allows users to navigate through mod links
/// while browsing in their external browser.
/// </summary>
public partial class FloatingCompanionWindow : Window
{
    private List<ModLink> _modLinks;
    private int _currentIndex;
    private int _expectedTotalCount; // The expected total count from modlist metadata
    private int _displayIndex; // The index to display (can be different from _currentIndex for multi-session mode)
    private readonly Action? _onNext;
    private readonly Action? _onSkip;
    private readonly Action? _onCancel;
    private readonly Action? _onFinishAndCopy;

    /// <summary>
    /// Creates a new floating companion window with callback-based navigation.
    /// </summary>
    /// <param name="modLinks">Current list of mod links</param>
    /// <param name="currentIndex">Current index in the list</param>
    /// <param name="expectedTotalCount">Expected total count of manual downloads (from modlist metadata)</param>
    /// <param name="onNext">Callback when Next is clicked</param>
    /// <param name="onSkip">Callback when Skip is clicked</param>
    /// <param name="onCancel">Callback when Cancel is clicked</param>
    /// <param name="onFinishAndCopy">Callback when Finish & Copy is clicked</param>
    /// <param name="displayIndex">Optional: The index to display (defaults to currentIndex + 1)</param>
    public FloatingCompanionWindow(List<ModLink> modLinks, int currentIndex, int expectedTotalCount, Action? onNext, Action? onSkip, Action? onCancel, Action? onFinishAndCopy, int displayIndex = 0)
    {
        InitializeComponent();
        
        _modLinks = modLinks ?? new List<ModLink>();
        _currentIndex = currentIndex;
        _expectedTotalCount = expectedTotalCount > 0 ? expectedTotalCount : _modLinks.Count;
        _displayIndex = displayIndex > 0 ? displayIndex : currentIndex + 1; // Use displayIndex if provided, else currentIndex + 1
        _onNext = onNext;
        _onSkip = onSkip;
        _onCancel = onCancel;
        _onFinishAndCopy = onFinishAndCopy;
        
        PreviousButton.Click += OnSkipClicked;
        NextButton.Click += OnNextClicked;
        ReturnButton.Click += OnCancelOrFinishClicked;
        
        // Update button labels for clarity
        PreviousButton.Content = "Skip";
        
        UpdateDisplay();
    }

    /// <summary>
    /// Legacy constructor for backward compatibility.
    /// </summary>
    public FloatingCompanionWindow(List<ModLink> modLinks, Action? onReturn = null)
        : this(modLinks, 0, 0, null, null, onReturn, onReturn)
    {
        // For legacy usage, wire up the old behavior
        PreviousButton.Click -= OnSkipClicked;
        NextButton.Click -= OnNextClicked;
        ReturnButton.Click -= OnCancelOrFinishClicked;
        
        PreviousButton.Click += OnLegacyPreviousClicked;
        NextButton.Click += OnLegacyNextClicked;
        ReturnButton.Click += (s, e) => { onReturn?.Invoke(); Close(); };
        
        PreviousButton.Content = "Previous";
        ReturnButton.Content = "Return";
        
        // Open the first mod link automatically
        if (_modLinks.Count > 0)
        {
            OpenCurrentModLink();
        }
    }

    /// <summary>
    /// Updates the mod list and current index. Used when new mods are added during processing.
    /// </summary>
    public void UpdateModList(List<ModLink> modLinks, int currentIndex, int expectedTotalCount = 0)
    {
        // Ensure we're on the UI thread for the update
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => UpdateModList(modLinks, currentIndex, expectedTotalCount));
            return;
        }
        
        _modLinks = modLinks ?? new List<ModLink>();
        _currentIndex = currentIndex;
        if (expectedTotalCount > 0)
        {
            _expectedTotalCount = expectedTotalCount;
        }
        UpdateDisplay();
    }

    /// <summary>
    /// Updates just the current index.
    /// </summary>
    public void UpdateCurrentIndex(int currentIndex)
    {
        _currentIndex = currentIndex;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_modLinks.Count == 0)
        {
            StatusText.Text = "No mods in list";
            CurrentModName.Text = "";
            PreviousButton.IsEnabled = false;
            NextButton.IsEnabled = false;
            ReturnButton.Content = "Cancel";
            return;
        }

        // Use _displayIndex for the "Mod X of Y" display
        // _displayIndex is the persistent counter across sessions
        var displayTotal = _expectedTotalCount > 0 ? _expectedTotalCount : _modLinks.Count;
        var statusText = $"Mod {_displayIndex} of {displayTotal}";
        StatusText.Text = statusText;
        
        // Debug: Log to console to verify update is happening
        System.Diagnostics.Debug.WriteLine($"[FloatingCompanionWindow] UpdateDisplay: {statusText}, ModName: {_modLinks[_currentIndex].Name}");
        
        CurrentModName.Text = _modLinks[_currentIndex].Name;
        
        // Skip button is always enabled
        PreviousButton.IsEnabled = true;
        
        // Next button is always enabled (clicking finishes current session)
        NextButton.IsEnabled = true;
        
        // Update button label based on position relative to expected total
        // If on last mod of expected total, show "Finish & Copy"
        // Otherwise show "Cancel"
        var isOnLastMod = _displayIndex >= _expectedTotalCount && _expectedTotalCount > 0;
        if (isOnLastMod)
        {
            ReturnButton.Content = "Finish && Copy";
        }
        else
        {
            ReturnButton.Content = "Cancel";
        }
    }

    private void OnNextClicked(object sender, RoutedEventArgs e)
    {
        _onNext?.Invoke();
    }

    private void OnSkipClicked(object sender, RoutedEventArgs e)
    {
        // Show confirmation dialog before skipping
        var result = MessageBox.Show(
            $"Are you sure you want to skip this mod?\n\n{_modLinks[_currentIndex].Name}",
            "Skip Mod",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            _onSkip?.Invoke();
        }
    }

    private void OnCancelOrFinishClicked(object sender, RoutedEventArgs e)
    {
        // Check if we're on the last mod of the expected total
        var isOnLastMod = _displayIndex >= _expectedTotalCount && _expectedTotalCount > 0;
        if (isOnLastMod)
        {
            _onFinishAndCopy?.Invoke();
        }
        else
        {
            // Otherwise this is "Cancel" - just cancel without file copy prompt
            _onCancel?.Invoke();
        }
    }

    // Legacy methods for backward compatibility
    private void OnLegacyPreviousClicked(object sender, RoutedEventArgs e)
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            UpdateDisplay();
            OpenCurrentModLink();
        }
    }

    private void OnLegacyNextClicked(object sender, RoutedEventArgs e)
    {
        if (_currentIndex < _modLinks.Count - 1)
        {
            _currentIndex++;
            UpdateDisplay();
            OpenCurrentModLink();
        }
    }

    private void OpenCurrentModLink()
    {
        if (_currentIndex >= 0 && _currentIndex < _modLinks.Count)
        {
            var link = _modLinks[_currentIndex];
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = link.Url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Opens the mod browser companion with the specified list of mod links.
    /// Legacy method for backward compatibility.
    /// </summary>
    public static FloatingCompanionWindow Show(List<ModLink> modLinks, Action? onReturn = null)
    {
        var window = new FloatingCompanionWindow(modLinks, onReturn);
        window.Show();
        return window;
    }
}

/// <summary>
/// Represents a mod link with a name and URL.
/// </summary>
public class ModLink
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    
    public ModLink() { }
    
    public ModLink(string name, string url)
    {
        Name = name;
        Url = url;
    }
}
