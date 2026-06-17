using ProjectDish.Core;
namespace ProjectDish.MVVM.Models
{
    public class UserModel : ViewModelBase
    {
        private int _id;
        private string _username;
        private string _email;
        private int _roleId;
        private string _roleName;
        private int _isBlocked;
        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }
        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }
        public int RoleId
        {
            get => _roleId;
            set
            {
                _roleId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RoleName));
            }
        }
        public string RoleName
        {
            get => _roleName;
            set { _roleName = value; OnPropertyChanged(); }
        }
        public int IsBlocked
        {
            get => _isBlocked;
            set
            {
                _isBlocked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BlockedText));
            }
        }
        public string BlockedText => IsBlocked == 1 ? "Да" : "Нет";
    }
}
