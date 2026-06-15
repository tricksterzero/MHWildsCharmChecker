using System.Windows;

namespace CharmChecker.App;

public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    public string ScreenshotFolder { get; private set; } = "";

    public SettingsWindow(string currentFolder)
    {
        InitializeComponent();
        ScreenshotFolder = currentFolder;
        FolderPathBox.Text = currentFolder;
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
        DialogResult = true;
    }
}
