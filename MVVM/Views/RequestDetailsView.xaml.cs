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
    /// Логика взаимодействия для RequestDetailsView.xaml
    /// </summary>
    public partial class RequestDetailsView : Window
    {
        public RequestDetailsView(int requestId)
        {
            InitializeComponent();
            DataContext = new RequestDetailsViewModel(requestId);
        }
    }
}
