using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Models.Component;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Range = SUNWODA_SEVB.Core.Models.Component.Range;

namespace SUNWODA_SEVB.ViewModels.Pages.HawkEye
{
    [Module("VM_MachineDamageMonitoringPage", "鹰眼机损监测")]
    public class VM_MachineDamageMonitoringPage : ViewModelBase
    {
        private ObservableCollection<Trajectory3D>? trajectories;
        private Trajectory3D? selectedTrajectory;
        private ICommand? trajectoryClickCommand;
        private Range xAxisRange;
        private Range yAxisRange;
        private Range zAxisRange;
        private string? xLabel;
        private string? yLabel;
        private string? zLabel;
        private double gridStep;
        private double axisLabelFontSize;
        private double gridThickness;
        private double tickLength;
        private double thresholdBoxHalfLength;
        private double thresholdBoxHalfWidth;
        private double thresholdBoxHalfHeight;
        private Point3D thresholdBoxCenter;
        private string? chartTitle;

        public VM_MachineDamageMonitoringPage()
        {
            Trajectories = new ObservableCollection<Trajectory3D>();
            XAxisRange = new Range(-25, 25);
            YAxisRange = new Range(-25, 25);
            ZAxisRange = new Range(0, 50);
            XLabel = "X方向距离";
            YLabel = "Y方向距离";
            ZLabel = "Z方向距离";
            GridStep = 10;
            AxisLabelFontSize = 20;
            GridThickness = 0.2;
            TickLength = 2;
            ThresholdBoxHalfLength = 10;
            ThresholdBoxHalfWidth = 10;
            ThresholdBoxHalfHeight = 10;
            ThresholdBoxCenter = new Point3D(0, 0, 10);
            ChartTitle = "";
            InitializeCommands();
            LoadSampleData();
        }

        public ObservableCollection<Trajectory3D>? Trajectories
        {
            get => trajectories;
            set => SetProperty(ref trajectories, value);
        }

        public Trajectory3D? SelectedTrajectory
        {
            get => selectedTrajectory;
            set => SetProperty(ref selectedTrajectory, value);
        }

        public ICommand? TrajectoryClickCommand
        {
            get => trajectoryClickCommand;
            set => SetProperty(ref trajectoryClickCommand, value);
        }

        public Range XAxisRange
        {
            get => xAxisRange;
            set => SetProperty(ref xAxisRange, value);
        }

        public Range YAxisRange
        {
            get => yAxisRange;
            set => SetProperty(ref yAxisRange, value);
        }

        public Range ZAxisRange
        {
            get => zAxisRange;
            set => SetProperty(ref zAxisRange, value);
        }

        public double GridStep
        {
            get => gridStep;
            set => SetProperty(ref gridStep, value);
        }

        public double AxisLabelFontSize
        {
            get => axisLabelFontSize;
            set => SetProperty(ref axisLabelFontSize, value);
        }

        public string? XLabel
        {
            get => xLabel;
            set => SetProperty(ref xLabel, value);
        }

        public string? YLabel
        {
            get => yLabel;
            set => SetProperty(ref yLabel, value);
        }

        public string? ZLabel
        {
            get => zLabel;
            set => SetProperty(ref zLabel, value);
        }

        public double GridThickness
        {
            get => gridThickness;
            set => SetProperty(ref gridThickness, value);
        }

        public double TickLength
        {
            get => tickLength;
            set => SetProperty(ref tickLength, value);
        }

        public double ThresholdBoxHalfLength
        {
            get => thresholdBoxHalfLength;
            set => SetProperty(ref thresholdBoxHalfLength, value);
        }

        public double ThresholdBoxHalfWidth
        {
            get => thresholdBoxHalfWidth;
            set => SetProperty(ref thresholdBoxHalfWidth, value);
        }

        public double ThresholdBoxHalfHeight
        {
            get => thresholdBoxHalfHeight;
            set => SetProperty(ref thresholdBoxHalfHeight, value);
        }

        public Point3D ThresholdBoxCenter
        {
            get => thresholdBoxCenter;
            set => SetProperty(ref thresholdBoxCenter, value);
        }

        public string? ChartTitle
        {
            get => chartTitle;
            set => SetProperty(ref chartTitle, value);
        }

        private void InitializeCommands()
        {
            TrajectoryClickCommand = new RelayCommand<Trajectory3D>(OnTrajectoryClick);
        }

        private void OnTrajectoryClick(Trajectory3D? trajectory)
        {
            if (trajectory != null)
            {
                HandyControl.Controls.MessageBox.Show(
                    $"轨迹信息:\n"
                        + $"名称: {trajectory.Name}\n"
                        + $"点数: {trajectory.Points.Count}\n"
                        + $"动态: {trajectory.IsDynamic}",
                    "轨迹详情"
                );
            }
        }

        private void LoadSampleData()
        {
            SimulateDynamicCornerPointTrajectory();
        }

        private async void SimulateDynamicCornerPointTrajectory()
        {
            var topLeftTrajectory = new Trajectory3D
            {
                Id = "topLeftTrajectory",
                Name = "上左角实际轨迹",
                Color = Colors.Green,
                Thickness = 0.4,
                IsDynamic = true,
                ShowAnimation = false,
            };

            var topRightTrajectory = new Trajectory3D
            {
                Id = "topRightTrajectory",
                Name = "上右角实际轨迹",
                Color = Colors.Blue,
                Thickness = 0.4,
                IsDynamic = true,
                ShowAnimation = false,
            };

            var bottomLeftTrajectory = new Trajectory3D
            {
                Id = "bottomLeftTrajectory",
                Name = "下左角实际轨迹",
                Color = Colors.Yellow,
                Thickness = 0.4,
                IsDynamic = true,
                ShowAnimation = false,
            };

            var bottomRightTrajectory = new Trajectory3D
            {
                Id = "bottomRightTrajectory",
                Name = "下右角实际轨迹",
                Color = Colors.Purple,
                Thickness = 0.4,
                IsDynamic = true,
                ShowAnimation = false,
            };
            Trajectories?.Add(topLeftTrajectory);
            Trajectories?.Add(topRightTrajectory);
            Trajectories?.Add(bottomLeftTrajectory);
            Trajectories?.Add(bottomRightTrajectory);

            var task = Task.Run(async () =>
            {
                for (int i = 0; i <= 50; i++)
                {
                    await Task.Delay(500);

                    RunOnUIThread(() =>
                    {
                        double t = i * 0.5;
                        topLeftTrajectory.Points.Add(
                            new TrajectoryPoint3D
                            {
                                Position = new Point3D(10 * Math.Sin(t * 0.2) + 2, -6, 25 - t),
                                Timestamp = DateTime.Now,
                            }
                        );
                    });
                }
            });

            var task2 = Task.Run(async () =>
            {
                for (int i = 0; i <= 50; i++)
                {
                    await Task.Delay(500);

                    RunOnUIThread(() =>
                    {
                        double t = i * 0.5;
                        topRightTrajectory.Points.Add(
                            new TrajectoryPoint3D
                            {
                                Position = new Point3D(10 * Math.Sin(t * 0.2) + 2, -2, 25 - t),
                                Timestamp = DateTime.Now,
                            }
                        );
                    });
                }
            });

            var task3 = Task.Run(async () =>
            {
                for (int i = 0; i <= 50; i++)
                {
                    await Task.Delay(500);

                    RunOnUIThread(() =>
                    {
                        double t = i * 0.5;
                        bottomLeftTrajectory.Points.Add(
                            new TrajectoryPoint3D
                            {
                                Position = new Point3D(10 * Math.Sin(t * 0.2) + 2, 2, 25 - t),
                                Timestamp = DateTime.Now,
                            }
                        );
                    });
                }
            });

            var task4 = Task.Run(async () =>
            {
                for (int i = 0; i <= 50; i++)
                {
                    await Task.Delay(500);

                    RunOnUIThread(() =>
                    {
                        double t = i * 0.5;
                        bottomRightTrajectory.Points.Add(
                            new TrajectoryPoint3D
                            {
                                Position = new Point3D(10 * Math.Sin(t * 0.2) + 2, 6, 25 - t),
                                Timestamp = DateTime.Now,
                            }
                        );
                    });
                }
            });

            await Task.WhenAll(task, task2, task3, task4);
        }
    }
}
