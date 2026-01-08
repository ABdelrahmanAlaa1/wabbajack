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
    private readonly Action? _onNext;
    private readonly Action? _onSkip;
    private readonly Action? _onFinish;

    /// <summary>
    /// Creates a new floating companion window with callback-based navigation.
    /// </summary>
    public FloatingCompanionWindow(List<ModLink> modLinks, int currentIndex, Action? onNext, Action? onSkip, Action? onFinish)
    {
        InitializeComponent();
        
        _modLinks = modLinks ?? new List<ModLink>();
        _currentIndex = currentIndex;
        _onNext = onNext;
        _onSkip = onSkip;
        _onFinish = onFinish;
        
        PreviousButton.Click += OnSkipClicked;
        NextButton.Click += OnNextClicked;
        ReturnButton.Click += OnFinishClicked;
        
        // Update button labels for clarity
        PreviousButton.Content = "Skip";
        ReturnButton.Content = "Finish All";
        
        UpdateDisplay();
    }

    /// <summary>
    /// Legacy constructor for backward compatibility.
    /// </summary>
    public FloatingCompanionWindow(List<ModLink> modLinks, Action? onReturn = null)
        : this(modLinks, 0, null, null, onReturn)
    {
        // For legacy usage, wire up the old behavior
        PreviousButton.Click -= OnSkipClicked;
        NextButton.Click -= OnNextClicked;
        ReturnButton.Click -= OnFinishClicked;
        
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
    public void UpdateModList(List<ModLink> modLinks, int currentIndex)
    {
        _modLinks = modLinks ?? new List<ModLink>();
        _currentIndex = currentIndex;
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
            return;
        }

        StatusText.Text = $"Mod {_currentIndex + 1} of {_modLinks.Count}";
        CurrentModName.Text = _modLinks[_currentIndex].Name;
        
        // Skip button is always enabled
        PreviousButton.IsEnabled = true;
        // Next button is enabled if there are more mods
        NextButton.IsEnabled = _currentIndex < _modLinks.Count - 1;
    }

    private void OnNextClicked(object sender, RoutedEventArgs e)
    {
        _onNext?.Invoke();
    }

    private void OnSkipClicked(object sender, RoutedEventArgs e)
    {
        _onSkip?.Invoke();
    }

    private void OnFinishClicked(object sender, RoutedEventArgs e)
    {
        _onFinish?.Invoke();
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
