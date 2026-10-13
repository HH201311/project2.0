using SUNWODA_SEVB.Core.Common;

namespace SUNWODA_SEVB.Core.Models.Project.BoxGluing
{
    /// <summary>
    /// 监测页底部实时数值：与曲线同一快照的最新点。
    /// </summary>
    public sealed class BoxGluingStationRealtime : ModelBase
    {
        private bool _isActive;
        private string _torqueA = "--";
        private string _torqueB = "--";
        private string _pressureA = "--";
        private string _pressureB = "--";
        private string _meteringPressureA = "--";
        private string _meteringPressureB = "--";
        private string _levelA = "--";
        private string _levelB = "--";

        public BoxGluingStationRealtime(int station)
        {
            Station = station;
        }

        public int Station { get; }

        public string Title => $"出胶{Station}";

        public bool IsActive
        {
            get => _isActive;
            private set
            {
                if (SetProperty(ref _isActive, value))
                    OnPropertyChanged(nameof(StateText));
            }
        }

        public string StateText => IsActive ? "采集中" : "待机";

        public string TorqueA
        {
            get => _torqueA;
            private set => SetProperty(ref _torqueA, value);
        }

        public string TorqueB
        {
            get => _torqueB;
            private set => SetProperty(ref _torqueB, value);
        }

        public string PressureA
        {
            get => _pressureA;
            private set => SetProperty(ref _pressureA, value);
        }

        public string PressureB
        {
            get => _pressureB;
            private set => SetProperty(ref _pressureB, value);
        }

        public string MeteringPressureA
        {
            get => _meteringPressureA;
            private set => SetProperty(ref _meteringPressureA, value);
        }

        public string MeteringPressureB
        {
            get => _meteringPressureB;
            private set => SetProperty(ref _meteringPressureB, value);
        }

        public string LevelA
        {
            get => _levelA;
            private set => SetProperty(ref _levelA, value);
        }

        public string LevelB
        {
            get => _levelB;
            private set => SetProperty(ref _levelB, value);
        }

        public void Apply(
            bool isActive,
            double? torqueA,
            double? torqueB,
            double? pressureA,
            double? pressureB,
            double? meteringPressureA,
            double? meteringPressureB,
            double? levelA,
            double? levelB)
        {
            IsActive = isActive;
            TorqueA = Format(torqueA);
            TorqueB = Format(torqueB);
            PressureA = Format(pressureA);
            PressureB = Format(pressureB);
            MeteringPressureA = Format(meteringPressureA);
            MeteringPressureB = Format(meteringPressureB);
            LevelA = Format(levelA);
            LevelB = Format(levelB);
        }

        private static string Format(double? value) =>
            value.HasValue ? value.Value.ToString("0.#") : "--";
    }
}
