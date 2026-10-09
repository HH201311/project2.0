using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SUNWODA_SEVB.Core.Models.Project.NJSIX
{
    /// <summary>工位统计行（UI 绑定）。</summary>
    public sealed class Pack6StationStat : INotifyPropertyChanged
    {
        private int _total;
        private int _ok;
        private int _ng;
        private string _rate = "0%";

        public string Station { get; }

        public int Total
        {
            get => _total;
            set
            {
                if (_total == value) return;
                _total = value;
                OnPropertyChanged();
                RecalcRate();
            }
        }

        public int OK
        {
            get => _ok;
            set
            {
                if (_ok == value) return;
                _ok = value;
                OnPropertyChanged();
                RecalcRate();
            }
        }

        public int NG
        {
            get => _ng;
            set
            {
                if (_ng == value) return;
                _ng = value;
                OnPropertyChanged();
                RecalcRate();
            }
        }

        public string Rate
        {
            get => _rate;
            private set
            {
                if (_rate == value) return;
                _rate = value;
                OnPropertyChanged();
            }
        }

        public Pack6StationStat(string station) => Station = station;

        public void Clear()
        {
            Total = 0;
            OK = 0;
            NG = 0;
        }

        private void RecalcRate()
        {
            if (Total <= 0)
            {
                Rate = "0%";
                return;
            }
            Rate = $"{(double)NG / Total:P1}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
