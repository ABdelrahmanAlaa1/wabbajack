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
    private readonly List<ModLink> _modLinks;
    private int _currentIndex;
    private readonly Action? _onReturn;

    public FloatingCompanionWindow(List<ModLink> modLinks, Action? onReturn = null)
    {
        InitializeComponent();
        
        _modLinks = modLinks ?? new List<ModLink>();
        _currentIndex = 0;
        _onReturn = onReturn;
        
        PreviousButton.Click += OnPreviousClicked;
        NextButton.Click += OnNextClicked;
        ReturnButton.Click += OnReturnClicked;
        
        UpdateDisplay();
        
        // Open the first mod link automatically
        if (_modLinks.Count > 0)
        {
            OpenCurrentModLink();
        }
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
        
        PreviousButton.IsEnabled = _currentIndex > 0;
        NextButton.IsEnabled = _currentIndex < _modLinks.Count - 1;
    }

    private void OnPreviousClicked(object sender, RoutedEventArgs e)
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            UpdateDisplay();
            OpenCurrentModLink();
        }
    }

    private void OnNextClicked(object sender, RoutedEventArgs e)
    {
        if (_currentIndex < _modLinks.Count - 1)
        {
            _currentIndex++;
            UpdateDisplay();
            OpenCurrentModLink();
        }
    }

    private void OnReturnClicked(object sender, RoutedEventArgs e)
    {
        _onReturn?.Invoke();
        Close();
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
