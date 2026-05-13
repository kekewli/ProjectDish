using ProjectDish.MVVM.ViewModels;
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
    /// Логика взаимодействия для RecipeView.xaml
    /// </summary>
    public partial class RecipeView : Window
    {
        private readonly RecipeViewModel _viewModel;

        // Конструктор, который принимает все нужные параметры
        public RecipeView(int recipeId = -1, int userId = -1, bool isRequest = false)
        {
            InitializeComponent();
            _viewModel = new RecipeViewModel(recipeId, userId, isRequest);
            DataContext = _viewModel;
        }

        private void Border_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (DataContext is RecipeViewModel vm)
                {
                    vm.HandleImageDrop(files);
                }
            }
        }
    }
}
