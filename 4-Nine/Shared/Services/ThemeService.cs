namespace Nine.Shared.Services;

public class ThemeService
{
    private string _currentTheme = "light";
    private string _currentBrandTheme = "obsidian";
    
    public event Action? OnThemeChanged;
    public event Action? OnBrandThemeChanged;
    
    public string CurrentTheme => _currentTheme;
    public string CurrentBrandTheme => _currentBrandTheme;
    
    public void SetTheme(string theme)
    {
        if (theme != "light" && theme != "dark")
        {
            throw new ArgumentException("Theme must be 'light' or 'dark'", nameof(theme));
        }
        
        _currentTheme = theme;
        OnThemeChanged?.Invoke();
    }
    
    public void ToggleTheme()
    {
        SetTheme(_currentTheme == "light" ? "dark" : "light");
    }
    
    public string GetNextTheme()
    {
        return _currentTheme == "light" ? "dark" : "light";
    }
    
    // Valid brand themes - add new themes here when implementing them
    private readonly HashSet<string> _validBrandThemes = new()
    {
        "obsidian",
        "bootstrap",
        "teal"
    };

    public void SetBrandTheme(string brandTheme)
    {
        if (!_validBrandThemes.Contains(brandTheme))
        {
            throw new ArgumentException($"Brand theme must be one of: {string.Join(", ", _validBrandThemes)}", nameof(brandTheme));
        }
        
        _currentBrandTheme = brandTheme;
        OnBrandThemeChanged?.Invoke();
    }
}
