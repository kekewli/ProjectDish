using ProjectDish.Core;

namespace ProjectDish.MVVM.Models
{
    class RequestModel : ViewModelBase
    {
        public int Id { get; set; }
        public string RecipeName { get; set; }
        public string ImageUrl { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
    }
}
