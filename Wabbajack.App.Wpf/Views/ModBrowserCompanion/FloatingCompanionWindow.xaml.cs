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
    public FloatingCompanionWindow(List<ModLink> modLinks, int currentIndex, int expectedTotalCount, Action? onNext, Action? onSkip, Action? onCancel, Action? onFinishAndCopy)
    {
        InitializeComponent();
        
        _modLinks = modLinks ?? new List<ModLink>();
        _currentIndex = currentIndex;
        _expectedTotalCount = expectedTotalCount > 0 ? expectedTotalCount : _modLinks.Count;
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

        // Use the expected total count if known, otherwise show current count
        // This shows "Mod 1 of 1166" from the start when expected total is known
        var displayTotal = _expectedTotalCount > _modLinks.Count ? _expectedTotalCount : _modLinks.Count;
        StatusText.Text = $"Mod {_currentIndex + 1} of {displayTotal}";
        CurrentModName.Text = _modLinks[_currentIndex].Name;
        
        // Skip button is always enabled
        PreviousButton.IsEnabled = true;
        
        // Next button is enabled if there are more mods in the current list
        // OR if we expect more mods to arrive based on the expected total
        var hasMoreModsInList = _currentIndex < _modLinks.Count - 1;
        var expectingMoreMods = _expectedTotalCount > 0 && _modLinks.Count < _expectedTotalCount;
        NextButton.IsEnabled = hasMoreModsInList || expectingMoreMods;
        
        // Update button label based on position relative to expected total
        // If on last mod of expected total (or current list if no expected total), show "Finish & Copy"
        // Otherwise show "Cancel"
        var isOnLastMod = (_currentIndex >= _modLinks.Count - 1) && (_modLinks.Count >= _expectedTotalCount || _expectedTotalCount == 0);
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
        // Invoke skip callback directly (no confirmation dialog for easy auto-clicking)
        _onSkip?.Invoke();
    }

    private void OnCancelOrFinishClicked(object sender, RoutedEventArgs e)
    {
        // Check if we're on the last mod of the expected total
        var isOnLastMod = (_currentIndex >= _modLinks.Count - 1) && (_modLinks.Count >= _expectedTotalCount || _expectedTotalCount == 0);
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
