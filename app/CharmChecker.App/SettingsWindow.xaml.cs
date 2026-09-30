using System.Windows;

namespace CharmChecker.App;

public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    /// <summary>ThemeComboBoxの項目の並び順(SettingsWindow.xamlと対応)。</summary>
    private static readonly AppThemeMode[] ThemeOrder = [AppThemeMode.System, AppThemeMode.Light, AppThemeMode.Dark];

    public string ScreenshotFolder { get; private set; } = "";
    public AppThemeMode SelectedTheme { get; private set; }

    public SettingsWindow(string currentFolder, AppThemeMode currentTheme)
    {
        InitializeComponent();
        ScreenshotFolder = currentFolder;
        FolderPathBox.Text = currentFolder;
        SelectedTheme = currentTheme;
        ThemeComboBox.SelectedIndex = Array.IndexOf(ThemeOrder, currentTheme);
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog();
        if (!string.IsNullOrEmpty(FolderPathBox.Text))
            dialog.InitialDirectory = FolderPathBox.Text;
        if (dialog.ShowDialog() == true)
            FolderPathBox.Text = dialog.FolderName;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        ScreenshotFolder = FolderPathBox.Text;
        if (ThemeComboBox.SelectedIndex >= 0)
            SelectedTheme = ThemeOrder[ThemeComboBox.SelectedIndex];
        DialogResult = true;
    }
}
