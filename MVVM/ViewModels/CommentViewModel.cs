using ProjectDish.Core;
namespace ProjectDish.MVVM.ViewModels
{
    internal class CommentViewModel : ViewModelBase
    {
        private int _rating;
        private int _userVote; 
        public int CommentId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string CommentText { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Rating
        {
            get => _rating;
            set { _rating = value; OnPropertyChanged(); }
        }
        public int UserVote
        {
            get => _userVote;
            set { _userVote = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsUpvoted)); OnPropertyChanged(nameof(IsDownvoted)); }
        }
        // Свойства для подсветки кнопок
        public bool IsUpvoted => UserVote == 1;
        public bool IsDownvoted => UserVote == -1;
        // Команды для оценки и удаления
        public RelayCommand VoteCommand { get; set; }
        public RelayCommand DeleteCommand { get; set; }
    }
}
