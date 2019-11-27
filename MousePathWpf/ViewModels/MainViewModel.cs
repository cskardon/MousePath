namespace XClave.MousePath.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Reactive.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Forms;
    using System.Windows.Input;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
    using GalaSoft.MvvmLight.Messaging;
    using Gma.System.MouseKeyHook;
    using Microsoft.Win32;
    using Application = System.Windows.Application;
    using Image = System.Windows.Controls.Image;

    public class MainViewModel : ViewModelBase
    {
        private readonly ICollection<string> _filesToTryToDelete;
        //private readonly IDisposable _mouseMoveSubscriber;

        private readonly string _tempPictureFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + "\\MousePath\\";

        private readonly Window _window;


        private string _currentCachedImage;
        private int _mouseX;
        private int _mouseY;

        private bool _wasCurrentCachedImageExported;

        public MainViewModel(Window window)
        {
            _filesToTryToDelete = new List<string>();
            _window = window;

            var dInfo = new DirectoryInfo(_tempPictureFolder);
            if (!dInfo.Exists)
                dInfo.Create();

            ScreenLockFix();
            SetupCommands();

            Observable
                .FromEventPattern<EventHandler<MouseEventExtArgs>, MouseEventExtArgs>
                (h => Hook.GlobalEvents().MouseMoveExt += h, h => Hook.GlobalEvents().MouseMoveExt -= h)
                .Throttle(TimeSpan.FromMilliseconds(2))
                .Select(e => e.EventArgs)
                .Subscribe(MouseMoved);
        }

        public string CurrentCachedImage
        {
            get { return _currentCachedImage; }
            set
            {
                _currentCachedImage = value;
                RaisePropertyChanged("CurrentCachedImage");
            }
        }

        public ICommand ExitCommand { get; private set; }
        public ICommand ExportCommand { get; private set; }
        public ICommand ResetCommand { get; private set; }
        public ICommand ResizeCommand { get; private set; }

        public int MouseX
        {
            get { return _mouseX; }
            set
            {
                _mouseX = value;
                RaisePropertyChanged("MouseX");
            }
        }

        public int MouseY
        {
            get { return _mouseY; }
            set
            {
                _mouseY = value;
                RaisePropertyChanged("MouseY");
            }
        }

        private void SetupCommands()
        {
            ExitCommand = new RelayCommand(Exit);
            ResetCommand = new RelayCommand<Canvas>(c => c.Children.Clear());
            ExportCommand = new RelayCommand<Grid>(
                g => Messenger.Default.Send(
                    new NotificationMessageAction<string>(
                        this, "Export",
                        s => Export(g, false, s)), "MainViewModel"));

            ResizeCommand = new RelayCommand<Window>(ResizeCommandExecuted);
        }

        /// <summary>Ensure that when the screen is unlocked (on a multi-screen system) the canvas is sized correctly.</summary>
        private void ScreenLockFix()
        {
            SystemEvents.SessionSwitch +=
                (s, e) =>
                    {
                        if (e.Reason
                            == SessionSwitchReason.SessionUnlock)
                            ResizeCommand.Execute(_window);
                    };
        }

        private void MouseMoved(MouseEventExtArgs e)
        {
            MouseX = e.X;
            MouseY = e.Y;
            Messenger.Default.Send(
                new NotificationMessage<Tuple<MouseEventExtArgs, MouseEventExtArgs>>
                    (this, new Tuple<MouseEventExtArgs, MouseEventExtArgs>(e, e), "MouseMoved"), "MainViewModel");
        }

        public override void Cleanup()
        {
            if (Directory.Exists(_tempPictureFolder))
            {
                foreach (string file in Directory.GetFiles(_tempPictureFolder, "mousePath.temp.*"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (IOException)
                    {
                        Debug.WriteLine("Couldn't delete the file: " + file);
                    }
                }
            }

            base.Cleanup();
        }

        private static void ResizeCommandExecuted(Window window)
        {
            Rectangle r = GetUnionOfScreens(Screen.AllScreens);
            if (Math.Abs(window.Height - window.ActualHeight)
                > double.Epsilon) window.Height = r.Height + 1;
            if (Math.Abs(window.Width - window.ActualWidth)
                > double.Epsilon) window.Width = r.Width + 1;

            window.Height = r.Height;
            window.Width = r.Width;
            window.Top = 0;
            window.Left = 0;
        }

        private static Rectangle GetUnionOfScreens(Screen[] allScreens)
        {
            if (allScreens.Length == 1)
                return allScreens[0].Bounds;

            Rectangle current = allScreens[0].Bounds;
            for (int i = 1; i < allScreens.Length; i++)
                current = Rectangle.Union(current, allScreens[i].Bounds);

            return current;
        }

        //Get this from config...

        internal void Export(Grid g, bool deletePrevious, string filename = null)
        {
            string tempFile = string.IsNullOrEmpty(CurrentCachedImage) ? null : CurrentCachedImage.Replace("file:///", "");

            if (filename == null)
                filename = _tempPictureFolder + "mousePath.temp." + DateTime.Now.Ticks + ".png";

            //put image onto canvas before exporting...
            var location = new Uri(filename);

            Canvas canvas =
                g.Children.Cast<object>().Where(child => child.GetType() == typeof (Canvas)).Cast<Canvas>().FirstOrDefault();

            Image image =
                g.Children.Cast<object>().Where(child => child.GetType() == typeof (Image)).Cast<Image>().FirstOrDefault();

            if (canvas == null)
                return;

            if (image != null)
            {
                g.Children.Remove(image);
                canvas.Children.Add(image);
            }

            MousePath.Export.ToPng(location, canvas);
            CurrentCachedImage = location.ToString();
            ResetCommand.Execute(canvas);
            if (image != null)
                g.Children.Insert(0, image);

            if (deletePrevious && !string.IsNullOrEmpty(tempFile)
                && !_wasCurrentCachedImageExported)
                _filesToTryToDelete.Add(tempFile);

            _wasCurrentCachedImageExported = !deletePrevious;
            TryToDeleteFiles();
        }

        private void TryToDeleteFiles()
        {
            var deleted = new List<string>();
            foreach (string file in _filesToTryToDelete)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                        deleted.Add(file);
                    }
                }
                catch (IOException)
                {
                }
            }
        }

        private static void Exit()
        {
            Application.Current.Shutdown();
        }
    }
}