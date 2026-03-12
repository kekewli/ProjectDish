using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
