
using ProjectDish.MVVM.ViewModels;
using System.Windows;
using System.Windows.Controls;
using ProjectDish.MVVM.ViewModels;

namespace ProjectDish.MVVM.Views
{
    /// <summary>
    /// Логика взаимодействия для AdminView.xaml
    /// </summary>
    public partial class AdminView : Window
    {
        public AdminView()
        {
            InitializeComponent();
            DataContext = new AdminViewModel();
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
