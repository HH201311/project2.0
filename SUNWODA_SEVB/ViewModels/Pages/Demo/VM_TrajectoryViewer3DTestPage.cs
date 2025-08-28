using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Models.Component;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Range = SUNWODA_SEVB.Core.Models.Component.Range;

namespace SUNWODA_SEVB.ViewModels.Pages.Demo
{
    [Module("TrajectoryViewer3DTestPage", "3D 轨迹显示控件演示")]
    public class VM_TrajectoryViewer3DTestPage : ViewModelBase
    {
        private ObservableCollection<Trajectory3D>? trajectories;
        private Trajectory3D? selectedTrajectory;
        private ICommand? trajectoryClickCommand;
        private ICommand? startAnimationCommand;
        private ICommand? stopAnimationCommand;
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

        public VM_TrajectoryViewer3DTestPage()
        {
            Trajectories = new ObservableCollection<Trajectory3D>();
            XAxisRange = new Range(-25,25);
            YAxisRange = new Range(-25,25);
            ZAxisRange = new Range(0,50);
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
            ThresholdBoxCenter = new Point3D(0,0,10);
            ChartTitle = "角点运动轨迹监测图";
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

        public ICommand? StartAnimationCommand
        {
            get => startAnimationCommand;
            set => SetProperty(ref startAnimationCommand, value);
        }

        public ICommand? StopAnimationCommand
        {
            get => stopAnimationCommand;
            set => SetProperty(ref stopAnimationCommand, value);
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
            StartAnimationCommand = new RelayCommand(OnStartAnimation);
            StopAnimationCommand = new RelayCommand(OnStopAnimation);
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

        private void OnStartAnimation()
        {
            // 在View中处理
        }

        private void OnStopAnimation()
        {
            // 在View中处理
        }

        private void LoadSampleData()
        {
            //// 创建螺旋轨迹
            //var spiral = new Trajectory3D
            //{
            //    Id = "spiral",
            //    Name = "螺旋轨迹",
            //    Color = Colors.Blue,
            //    Thickness = 0.5,
            //    ShowAnimation = true,
            //    ShowKeyPoints = true,
            //};

            //for (int i = 0; i <= 100; i++)
            //{
            //    double t = i * 0.1;
            //    var point = new TrajectoryPoint3D
            //    {
            //        Position = new Point3D(10 * Math.Cos(t), 10 * Math.Sin(t), t * 2),
            //        Timestamp = DateTime.Now.AddSeconds(i),
            //        IsKeyPoint = i % 20 == 0,
            //        Label = i % 20 == 0 ? $"P{i}" : null,
            //    };
            //    spiral.Points.Add(point);
            //}

            //// 创建正弦波轨迹
            //var sine = new Trajectory3D
            //{
            //    Id = "sine",
            //    Name = "正弦波轨迹",
            //    Color = Colors.Green,
            //    Thickness = 0.3,
            //    ShowAnimation = true,
            //};

            //for (int i = 0; i <= 100; i++)
            //{
            //    double x = i * 0.5;
            //    var point = new TrajectoryPoint3D
            //    {
            //        Position = new Point3D(x - 25, 10 * Math.Sin(x * 0.2), 5 * Math.Cos(x * 0.2)),
            //        Timestamp = DateTime.Now.AddSeconds(i),
            //    };
            //    sine.Points.Add(point);
            //}

            //Trajectories?.Add(spiral);
            //Trajectories?.Add(sine);

            // 模拟动态轨迹
            //SimulateDynamicTrajectory();
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

        private async void SimulateDynamicTrajectory()
        {
            var dynamic = new Trajectory3D
            {
                Id = "dynamic",
                Name = "动态轨迹",
                Color = Colors.Green,
                Thickness = 0.4,
                IsDynamic = true,
                ShowAnimation = false,
            };

            var dynamic2 = new Trajectory3D
            {
                Id = "dynamic2",
                Name = "动态轨迹2",
                Color = Colors.Blue,
                Thickness = 0.4,
                IsDynamic = true,
                ShowAnimation = false,
            };

            Trajectories?.Add(dynamic);
            Trajectories?.Add(dynamic2);

            var task = Task.Run(async () =>
            {
                for (int i = 0; i <= 100; i++)
                {
                    await Task.Delay(500);

                    RunOnUIThread(() =>
                    {
                        double t = i * 0.5;
                        dynamic.Points.Add(
                            new TrajectoryPoint3D
                            {
                                Position = new Point3D(t - 25, 10 * Math.Sin(t * 0.2), 5 * Math.Cos(t * 0.2) + 10),
                                Timestamp = DateTime.Now,
                            }
                        );
                    });
                }
            });

            var task2 = Task.Run(async () =>
            {
                for (int i = 0; i <= 100; i++)
                {
                    await Task.Delay(500);

                    RunOnUIThread(() =>
                    {
                        double t = i * 0.5;
                        dynamic2.Points.Add(
                            new TrajectoryPoint3D
                            {
                                Position = new Point3D(10 * Math.Sin(t * 0.2), t - 25, 5 * Math.Cos(t * 0.2) + 10),
                                Timestamp = DateTime.Now,
                            }
                        );
                    });
                }
            });

            await Task.WhenAll(task, task2);

            //// 模拟实时数据
            //await Task.Run(async () =>
            //{
            //    //for (int i = 0; i < 50; i++)
            //    //{
            //    //    await Task.Delay(500);

            //    //    RunOnUIThread(() =>
            //    //    {
            //    //        double t = i * 0.2;
            //    //        dynamic.Points.Add(
            //    //            new TrajectoryPoint3D
            //    //            {
            //    //                Position = new Point3D(
            //    //                    15 * Math.Cos(t * 0.5),
            //    //                    15 * Math.Sin(t * 0.5),
            //    //                    -10 + i * 0.5
            //    //                ),
            //    //                Timestamp = DateTime.Now,
            //    //            }
            //    //        );
            //    //    });
            //    //}

            //    for (int i = 0; i <= 100; i++)
            //    {
            //        await Task.Delay(500);

            //        RunOnUIThread(() =>
            //        {
            //            double t = i * 0.5;
            //            dynamic.Points.Add(
            //                new TrajectoryPoint3D
            //                {
            //                    Position = new Point3D(t - 25, 10 * Math.Sin(t * 0.2), 5 * Math.Cos(t * 0.2) + 10),
            //                    Timestamp = DateTime.Now,
            //                }
            //            );
            //        });
            //    }
            //});
        }
    }
}
