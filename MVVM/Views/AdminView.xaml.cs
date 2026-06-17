
using ProjectDish.MVVM.ViewModels;
using System.Windows;
using System.Windows.Controls;
using ProjectDish.MVVM.ViewModels;
using ProjectDish.Core;

namespace ProjectDish.MVVM.Views
{
    /// <summary>
    /// Логика взаимодействия для AdminView.xaml
    /// </summary>
    public partial class AdminView : Window
    {
        public AdminView(int userId)
        {
            InitializeComponent();
            DataContext = new AdminViewModel(userId);
            FadeWindowBehavior.Attach(this);
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
