using ProjectDish.Core;
using System.Windows;

namespace ProjectDish.MVVM.Views
{
    /// <summary>
    /// Логика взаимодействия для LoginView.xaml
    /// </summary>
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();
            FadeWindowBehavior.Attach(this);
        }
        private void TogglePassBtn_Click(object sender, RoutedEventArgs e)
        {
            if (TogglePassBtn.IsChecked == true)
            {
                VisiblePasswordBox.Text = UserPasswordBox.Password;
                VisiblePasswordBox.Visibility = Visibility.Visible;
                UserPasswordBox.Visibility = Visibility.Hidden;
                TogglePassBtn.Content = "✖";
            }
            else
            {
                UserPasswordBox.Password = VisiblePasswordBox.Text;
                UserPasswordBox.Visibility = Visibility.Visible;
                VisiblePasswordBox.Visibility = Visibility.Collapsed;
                TogglePassBtn.Content = "👁";
            }
        }
    }
}
