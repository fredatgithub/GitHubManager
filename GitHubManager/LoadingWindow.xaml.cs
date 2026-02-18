using System.Windows;

namespace GitHubManager
{
  /// <summary>
  /// Logique d'interaction pour LoadingWindow.xaml
  /// </summary>
  public partial class LoadingWindow : Window
  {
    public LoadingWindow()
    {
      InitializeComponent();
    }

    public void UpdateStatus(string message)
    {
      if (StatusTextBlock != null)
      {
        StatusTextBlock.Text = message;
      }
    }
  }
}
