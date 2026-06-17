using ProjectDish.Core;
using ProjectDish.MVVM.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace ProjectDish.MVVM.Views
{
    /// <summary>
    /// Логика взаимодействия для RecipeDetailsView.xaml
    /// </summary>
    public partial class RecipeDetailsView : Window
    {
        private readonly RecipeDetailsViewModel _viewModel;
        public RecipeDetailsView(int recipeId, int userId, bool isAdmin)
        {
            InitializeComponent();
            _viewModel = new RecipeDetailsViewModel(recipeId, userId, isAdmin);
            DataContext = _viewModel;
            FadeWindowBehavior.Attach(this);
        }
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _viewModel.OnWindowClosing();
        }
    }
}
