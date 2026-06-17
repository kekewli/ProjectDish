using ProjectDish.Core;
using ProjectDish.MVVM.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// Логика взаимодействия для UserStorageView.xaml
    /// </summary>
    public partial class UserStorageView : Window
    {
        private readonly UserStorageViewModel _viewModel;

        public UserStorageView(int userId)
        {
            InitializeComponent();
            _viewModel = new UserStorageViewModel(userId);
            DataContext = _viewModel;
            FadeWindowBehavior.Attach(this);
        }
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _viewModel.OnWindowClosing();
        }
    }
}
