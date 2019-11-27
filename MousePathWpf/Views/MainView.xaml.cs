namespace XClave.MousePath
{
    using System;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using GalaSoft.MvvmLight.Messaging;
    using Gma.System.MouseKeyHook;
    using Microsoft.Win32;
    using XClave.MousePath.ViewModels;

    public partial class MainView : Window
    {
        private readonly RadialGradientBrush _radialGradientBrush
            = new RadialGradientBrush
                  {
                      GradientOrigin = new Point(0.5, 0.5),
                      Center = new Point(0.5, 0.5),
                      RadiusX = 0.5,
                      RadiusY = 0.5,
                      GradientStops = new GradientStopCollection
                                          {
                                              new GradientStop(
                                                  new Color
                                                      {
                                                          ScA = 0.6f,
                                                          ScB = 0,
                                                          ScG = 0,
                                                          ScR = 0
                                                      }, 0.7),
                                              new GradientStop(Colors.Transparent, 1)
                                          }
                  };

        private int _ellipseMultiplier;
        private DateTime _lastStopped;
        private Point _prevPoint = new Point(-1, -1);

        public MainView()
        {
            InitializeComponent();
            DataContext = new MainViewModel(this);
            RegisterForMessages();
            ContentRendered += (o, e) => Vm.ResizeCommand.Execute(this);
            Closing += (s, e) => Vm.Cleanup();
        }

        private MainViewModel Vm { get { return DataContext as MainViewModel; } }


        private void RegisterForMessages()
        {
            Messenger.Default.Register<NotificationMessage<Tuple<MouseEventExtArgs, MouseEventExtArgs>>>
                (
                    this, "MainViewModel", message =>
                                               {
                                                   switch (message.Notification.ToLowerInvariant())
                                                   {
                                                       case "mousemoved":
                                                           DrawPointsDelegate dpd = DrawPoints;
                                                           Dispatcher.BeginInvoke(dpd, message.Content);
                                                           break;
                                                       default:
                                                           throw new ArgumentOutOfRangeException("message", message.Notification, "Unknown message type: ");
                                                   }
                                               });

            Messenger.Default.Register<NotificationMessageAction<string>>
                (
                    this, "MainViewModel", message =>
                                               {
                                                   switch (message.Notification.ToLowerInvariant())
                                                   {
                                                       case "export":
                                                           GetExportLocation(message);
                                                           break;
                                                       default:
                                                           throw new ArgumentOutOfRangeException("message", message.Notification, "Unknown message type: ");
                                                   }
                                               });
        }

        private static void GetExportLocation(NotificationMessageAction<string> message)
        {
            var sfd = new SaveFileDialog
                          {
                              DefaultExt = "png",
                              InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                              RestoreDirectory = true,
                              CreatePrompt = true,
                              AddExtension = true,
                              Filter = "PNG Images (*.png)|*.png"
                          };
            if (sfd.ShowDialog().GetValueOrDefault())
                message.Execute(sfd.FileName);
        }

        private void DrawPoints(Tuple<MouseEventExtArgs, MouseEventExtArgs> content)
        {
            MouseEventExtArgs e = content.Item1;

            var secondsSinceStop = (int) (DateTime.Now - _lastStopped).TotalSeconds;
            if (secondsSinceStop > 0)
            {
                if (secondsSinceStop > 2)
                    Vm.Export(container, true);

                _ellipseMultiplier = secondsSinceStop;
            }
            else
            {
                if (_ellipseMultiplier > 0)
                {
                    double diameter = 10 * (1 + Math.Log(_ellipseMultiplier));
                    double radius = diameter / 2;


                    AddToCanvas(
                        new Ellipse
                            {
                                Height = diameter,
                                Width = diameter,
                                Stroke = Brushes.Black,
                                StrokeThickness = 1,
                                Fill = _radialGradientBrush,
                                Margin = new Thickness(_prevPoint.X - Left - radius, _prevPoint.Y - Top - radius, 0, 0)
                            });
                    _ellipseMultiplier = 0;
                }
            }

            if (e.X < Left || e.Y < Top || e.X > Left + ActualWidth
                || e.Y > Top + ActualHeight)
            {
                _prevPoint = new Point(-1, -1);
                return;
            }

            var currentPoint = new Point(e.X, e.Y);
            if (_prevPoint.X < 0
                && _prevPoint.Y < 0)
                _prevPoint = currentPoint;

            if (currentPoint.X < 0 || currentPoint.Y < 0 || _prevPoint.X < 0
                || _prevPoint.Y < 0)
                return;

            var l = new Line
                        {
                            X1 = _prevPoint.X - Left,
                            Y1 = _prevPoint.Y - Top,
                            X2 = currentPoint.X - Left,
                            Y2 = currentPoint.Y - Top,
                            Stroke = Brushes.Black,
                            StrokeThickness = 0.4
                        };

            _canvas.Children.Add(l);

            _prevPoint = currentPoint;
            _lastStopped = DateTime.Now;
        }

        private void AddToCanvas(UIElement element)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new DispatchToUiDelegate(AddToCanvas), element);
                return;
            }

            _canvas.Children.Add(element);
        }

        #region Nested type: DispatchToUiDelegate

        private delegate void DispatchToUiDelegate(UIElement e);

        #endregion

        #region Nested type: DrawPointsDelegate

        private delegate void DrawPointsDelegate(Tuple<MouseEventExtArgs, MouseEventExtArgs> points);

        #endregion
    }
}