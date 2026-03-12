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
        public RecipeView()
        {
            InitializeComponent();
            DataContext = new RecipeViewModel();
        }
        // Конструктор для редактирования
        public RecipeView(int recipeId)
        {
            InitializeComponent();
            DataContext = new RecipeViewModel(recipeId);
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
