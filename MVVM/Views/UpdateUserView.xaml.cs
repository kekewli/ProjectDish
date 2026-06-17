using ProjectDish.Core;
using ProjectDish.MVVM.ViewModels;
using System.Windows;

namespace ProjectDish.MVVM.Views
{
    /// <summary>
    /// Логика взаимодействия для UpdateUserView.xaml
    /// </summary>
    public partial class UpdateUserView : Window
    {
        public UpdateUserView(int userId)
        {
            InitializeComponent();
            DataContext = new UpdateUserViewModel(userId);
            FadeWindowBehavior.Attach(this);
        }
    }
}
