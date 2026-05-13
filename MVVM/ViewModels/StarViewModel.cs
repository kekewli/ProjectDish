using ProjectDish.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectDish.MVVM.ViewModels
{
    public class StarViewModel : ViewModelBase
    {
        private int _value;
        private bool _isFilled;

        public int Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        public bool IsFilled
        {
            get => _isFilled;
            set { _isFilled = value; OnPropertyChanged(); }
        }

        public StarViewModel(int value)
        {
            Value = value;
        }
    }
}
