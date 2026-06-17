using ProjectDish.Core;
namespace ProjectDish.MVVM.Models
{
    public class RecipeModel : ViewModelBase
    {
        private string _name;
        private string _imageUrl;
        private decimal _rating;
        public int Id { get; set; }
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }
        public string ImageUrl
        {
            get => _imageUrl;
            set { _imageUrl = value; OnPropertyChanged(); }
        }
        public decimal Rating
        {
            get => _rating;
            set { _rating = value; OnPropertyChanged(); }
        }
    }
}
