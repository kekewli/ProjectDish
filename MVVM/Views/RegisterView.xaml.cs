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
    public partial class RegisterView : Window
    {
        public RegisterView()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
        private void TogglePassBtn_Click(object sender, RoutedEventArgs e)
        {
            if (TogglePassBtn.IsChecked == true)
            {
                VisiblePasswordBox.Text = RegPasswordBox.Password;
                VisiblePasswordBox.Visibility = Visibility.Visible;
                RegPasswordBox.Visibility = Visibility.Hidden;
                TogglePassBtn.Content = "✖";
            }
            else
            {
                RegPasswordBox.Password = VisiblePasswordBox.Text;
                RegPasswordBox.Visibility = Visibility.Visible;
                VisiblePasswordBox.Visibility = Visibility.Collapsed;
                TogglePassBtn.Content = "👁";
            }
        }
        private void VisiblePasswordBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (VisiblePasswordBox.Visibility == Visibility.Visible)
            {
                RegPasswordBox.Password = VisiblePasswordBox.Text;
            }
        }
    }
}
