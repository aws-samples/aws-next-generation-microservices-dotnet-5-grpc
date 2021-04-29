using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace ModernTacoShop.WebApp.Models
{
    public class ExtraOption : INotifyPropertyChanged
    {

        public double Id { get; set; }
        public string Name { get; set; }
        public string DisplayString { get; set; }
        public decimal DecimalPlaces => 2;
        public int UnitAmount { get; set; }


        private bool isCheked;
        public bool IsChecked
        {
            get { return isCheked; }
            set
            {
                isCheked = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
    }
}
