using ProjectDish.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectDish.MVVM.Models
{
    class StarViewModel : ViewModelBase
    {
        private bool _isFilled;

        public int Value { get; }

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
