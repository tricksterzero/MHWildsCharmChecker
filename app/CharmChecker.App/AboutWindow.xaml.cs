using System.Windows;

namespace CharmChecker.App;

public partial class AboutWindow : Wpf.Ui.Controls.FluentWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = typeof(AboutWindow).Assembly.GetName().Version;
        VersionText.Text = version is not null
            ? $"バージョン {version.Major}.{version.Minor}.{version.Build}"
            : "バージョン 不明";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
