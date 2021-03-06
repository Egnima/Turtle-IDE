﻿using System;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Events;
using Microsoft.Practices.Prism.Modularity;
using Microsoft.Practices.Unity;
using Wide.Interfaces;
using Wide.Interfaces.Controls;
using Wide.Interfaces.Events;
using Wide.Interfaces.Services;
using Wide.Interfaces.Settings;
using Wide.Interfaces.Themes;
using Turtle_IDE.Core.Settings;
using System.Windows;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Diagnostics;
using Wide.Core.TextDocument;
using Microsoft.Win32;
using Turtle_IDE.Core.PythonView;
using System.Collections.Generic;
using System.Management;

namespace Turtle_IDE.Core
{
    [Module(ModuleName = "Turtle-IDE.Core")]
    [ModuleDependency("Wide.Tools.Logger")]
    [ModuleDependency("Turtle-IDE.Tools")]
    public class CoreModule : IModule
    {
        private IUnityContainer _container;
        private IEventAggregator _eventAggregator;

        public CoreModule(IUnityContainer container, IEventAggregator eventAggregator)
        {
            _container = container;
            _eventAggregator = eventAggregator;
        }

        public void Initialize()
        {
            _eventAggregator.GetEvent<SplashMessageUpdateEvent>().Publish(new SplashMessageUpdateEvent
            { Message = "Loading Core Module" });
            Application.Current.MainWindow.Closing += new CancelEventHandler(MainWindow_Closing);
            Application.Current.MainWindow.ContentRendered += new EventHandler(MainWindow_ContentRendered);
            Application.Current.MainWindow.Topmost = true;
            LoadTheme();
            LoadCommands();
            LoadMenus();
            LoadToolbar();
            RegisterParts();
            LoadSettings();

        }

        private static void KillProcessAndChildren(int pid)
        {
            // Cannot close 'system idle process'.
            if (pid == 0)
            {
                return;
            }
            ManagementObjectSearcher searcher = new ManagementObjectSearcher
                    ("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }
            try
            {
                Process proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
        }

        private int GetParentProcessId(Process p)
        {
            int parentId = 0;
            try
            {
                ManagementObject mo = new ManagementObject("win32_process.handle='" + p.Id + "'");
                mo.Get();
                parentId = Convert.ToInt32(mo["ParentProcessId"]);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                parentId = 0;
            }
            return parentId;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            //Process turtleIDE = Process.GetCurrentProcess();
            //int parentId = GetParentProcessId(turtleIDE);
            //KillProcessAndChildren(parentId);
            try
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo("taskkill", "/F /T /IM Turtle-IDE.exe")
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = System.AppDomain.CurrentDomain.BaseDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                Process.Start(processStartInfo);
            }
            catch { }
        }

        private void MainWindow_ContentRendered(object sender, EventArgs e)
        {
            Application.Current.MainWindow.Topmost = false;
        }

        private void LoadToolbar()
        {
            _eventAggregator.GetEvent<SplashMessageUpdateEvent>().Publish(new SplashMessageUpdateEvent
            { Message = "Toolbar.." });
            var toolbarService = _container.Resolve<IToolbarService>();
            var menuService = _container.Resolve<IMenuService>();
            var manager = _container.Resolve<ICommandManager>();

            toolbarService.Add(new ToolbarViewModel("Standard", 1) { Band = 1, BandIndex = 1 });
            toolbarService.Get("Standard").Add(menuService.Get("_File").Get("_New"));
            toolbarService.Get("Standard").Add(menuService.Get("_File").Get("_Open"));

            toolbarService.Add(new ToolbarViewModel("Edit", 1) { Band = 1, BandIndex = 2 });
            toolbarService.Get("Edit").Add(menuService.Get("_Edit").Get("_Undo"));
            toolbarService.Get("Edit").Add(menuService.Get("_Edit").Get("_Redo"));
            toolbarService.Get("Edit").Add(menuService.Get("_Edit").Get("Cut"));
            toolbarService.Get("Edit").Add(menuService.Get("_Edit").Get("Copy"));
            toolbarService.Get("Edit").Add(menuService.Get("_Edit").Get("_Paste"));

            toolbarService.Add(new ToolbarViewModel("Run", 1) { Band = 1, BandIndex = 2 });
            toolbarService.Get("Run").Add(new MenuItemViewModel("Run", 1, new BitmapImage(new Uri(@"pack://application:,,,/Turtle-IDE.Core;component/Icons/Play.png")), manager.GetCommand("RUN")));
            //toolbarService.Get("Run").Get("Run").Add(new MenuItemViewModel("Run with IronPython", 1, new BitmapImage(new Uri(@"pack://application:,,,/Turtle-IDE.Core;component/Icons/Play.png")), manager.GetCommand("RUN")));
            toolbarService.Get("Run").Get("Run").Add(new MenuItemViewModel("Run with Python3", 2, new BitmapImage(new Uri(@"pack://application:,,,/Turtle-IDE.Core;component/Icons/Play.png")), manager.GetCommand("RUN")));

            menuService.Get("_Tools").Add(toolbarService.RightClickMenu);

            //Initiate the position settings changes for toolbar
            _container.Resolve<IToolbarPositionSettings>();
        }

        private void LoadSettings()
        {
            ISettingsManager manager = _container.Resolve<ISettingsManager>();
            manager.Add(new IDESettingsItem("Text Editor", 1, null));
            manager.Get("Text Editor").Add(new IDESettingsItem("General", 1, EditorOptions.Default));
        }

        private void RegisterParts()
        {
            
            _container.RegisterType<PyCraftHandler>();
            _container.RegisterType<PyCraftViewModel>();
            _container.RegisterType<PyCraftView>();

            IContentHandler handler = _container.Resolve<PyCraftHandler>();
            _container.Resolve<IContentHandlerRegistry>().Register(handler);

            _container.RegisterType<PyHandler>();
            _container.RegisterType<PyViewModel>();
            _container.RegisterType<PyView>();

            handler = _container.Resolve<PyHandler>();
            _container.Resolve<IContentHandlerRegistry>().Register(handler);

        }

        private void LoadTheme()
        {
            _eventAggregator.GetEvent<SplashMessageUpdateEvent>().Publish(new SplashMessageUpdateEvent
            { Message = "Themes.." });
            var manager = _container.Resolve<IThemeManager>();
            var themeSettings = _container.Resolve<IThemeSettings>();
            var win = _container.Resolve<IShell>() as Window;
            manager.AddTheme(new LightTheme());
            manager.AddTheme(new DarkTheme());
            win.Dispatcher.InvokeAsync(() => manager.SetCurrent(themeSettings.SelectedTheme));
        }

        private void LoadCommands()
        {
            _eventAggregator.GetEvent<SplashMessageUpdateEvent>().Publish(new SplashMessageUpdateEvent
            { Message = "Commands.." });
            var manager = _container.Resolve<ICommandManager>();

            var openCommand = new DelegateCommand(OpenModule);
            var exitCommand = new DelegateCommand(CloseCommandExecute);
            var saveCommand = new DelegateCommand(SaveDocument, CanExecuteSaveDocument);
            var saveAsCommand = new DelegateCommand(SaveAsDocument, CanExecuteSaveAsDocument);
            var themeCommand = new DelegateCommand<string>(ThemeChangeCommand);
            var loggerCommand = new DelegateCommand(ToggleLogger);
            var toggleConsole = new DelegateCommand(ToggleConsole);
            var runCommand = new DelegateCommand(runPython);


            manager.RegisterCommand("OPEN", openCommand);
            manager.RegisterCommand("SAVE", saveCommand);
            manager.RegisterCommand("SAVEAS", saveAsCommand);
            manager.RegisterCommand("EXIT", exitCommand);
            manager.RegisterCommand("LOGSHOW", loggerCommand);
            manager.RegisterCommand("THEMECHANGE", themeCommand);
            manager.RegisterCommand("RUN", runCommand);
            manager.RegisterCommand("CONSOLESHOW", toggleConsole);
        }

        private void CloseCommandExecute()
        {
            IShell shell = _container.Resolve<IShell>();
            shell.Close();
        }

        private void LoadMenus()
        {
            _eventAggregator.GetEvent<SplashMessageUpdateEvent>().Publish(new SplashMessageUpdateEvent
            { Message = "Menus.." });
            var manager = _container.Resolve<ICommandManager>();
            var menuService = _container.Resolve<IMenuService>();
            var settingsManager = _container.Resolve<ISettingsManager>();
            var themeSettings = _container.Resolve<IThemeSettings>();
            var recentFiles = _container.Resolve<IRecentViewSettings>();
            IWorkspace workspace = _container.Resolve<AbstractWorkspace>();
            ToolViewModel logger = workspace.Tools.First(f => f.ContentId == "Logger");
            ToolViewModel console = workspace.Tools.First(f => f.ContentId == "Console");

            menuService.Add(new MenuItemViewModel("_File", 1));

            menuService.Get("_File").Add(
                (new MenuItemViewModel("_New", 3,
                                       new BitmapImage(
                                           new Uri(
                                               @"pack://application:,,,/Turtle-IDE.Core;component/Icons/NewRequest_8796.png")),
                                       manager.GetCommand("NEW"),
                                       new KeyGesture(Key.N, ModifierKeys.Control, "Ctrl + N"))));

            menuService.Get("_File").Add(
                (new MenuItemViewModel("_Open", 4,
                                       new BitmapImage(
                                           new Uri(
                                               @"pack://application:,,,/Turtle-IDE.Core;component/Icons/OpenFileDialog_692.png")),
                                       manager.GetCommand("OPEN"),
                                       new KeyGesture(Key.O, ModifierKeys.Control, "Ctrl + O"))));
            menuService.Get("_File").Add(new MenuItemViewModel("_Save", 5,
                                                               new BitmapImage(
                                                                   new Uri(
                                                                       @"pack://application:,,,/Turtle-IDE.Core;component/Icons/Save_6530.png")),
                                                               manager.GetCommand("SAVE"),
                                                               new KeyGesture(Key.S, ModifierKeys.Control, "Ctrl + S")));
            menuService.Get("_File").Add(new SaveAsMenuItemViewModel("Save As..", 6,
                                                   new BitmapImage(
                                                       new Uri(
                                                           @"pack://application:,,,/Turtle-IDE.Core;component/Icons/Save_6530.png")),
                                                   manager.GetCommand("SAVEAS"), null, false, false, _container));

            menuService.Get("_File").Add(new MenuItemViewModel("Close", 8, null, manager.GetCommand("CLOSE"),
                                                               new KeyGesture(Key.F4, ModifierKeys.Control, "Ctrl + F4")));

            menuService.Get("_File").Add(recentFiles.RecentMenu);

            menuService.Get("_File").Add(new MenuItemViewModel("E_xit", 101, null, manager.GetCommand("EXIT"),
                                                               new KeyGesture(Key.F4, ModifierKeys.Alt, "Alt + F4")));


            menuService.Add(new MenuItemViewModel("_Edit", 2));
            menuService.Get("_Edit").Add(new MenuItemViewModel("_Undo", 1,
                                                               new BitmapImage(
                                                                   new Uri(
                                                                       @"pack://application:,,,/Turtle-IDE.Core;component/Icons/Undo_16x.png")),
                                                               ApplicationCommands.Undo));
            menuService.Get("_Edit").Add(new MenuItemViewModel("_Redo", 2,
                                                               new BitmapImage(
                                                                   new Uri(
                                                                       @"pack://application:,,,/Turtle-IDE.Core;component/Icons/Redo_16x.png")),
                                                               ApplicationCommands.Redo));
            menuService.Get("_Edit").Add(MenuItemViewModel.Separator(15));
            menuService.Get("_Edit").Add(new MenuItemViewModel("Cut", 20,
                                                               new BitmapImage(
                                                                   new Uri(
                                                                       @"pack://application:,,,/Turtle-IDE.Core;component/Icons/Cut_6523.png")),
                                                               ApplicationCommands.Cut));
            menuService.Get("_Edit").Add(new MenuItemViewModel("Copy", 21,
                                                               new BitmapImage(
                                                                   new Uri(
                                                                       @"pack://application:,,,/Turtle-IDE.Core;component/Icons/Copy_6524.png")),
                                                               ApplicationCommands.Copy));
            menuService.Get("_Edit").Add(new MenuItemViewModel("_Paste", 22,
                                                               new BitmapImage(
                                                                   new Uri(
                                                                       @"pack://application:,,,/Turtle-IDE.Core;component/Icons/Paste_6520.png")),
                                                               ApplicationCommands.Paste));

            menuService.Add(new MenuItemViewModel("_View", 3));

            if (logger != null)
                menuService.Get("_View").Add(new MenuItemViewModel("_Logger", 1,
                                                                   new BitmapImage(
                                                                       new Uri(
                                                                           @"pack://application:,,,/Turtle-IDE.Core;component/Icons/Undo_16x.png")),
                                                                   manager.GetCommand("LOGSHOW"))
                { IsCheckable = true, IsChecked = logger.IsVisible });

            if (console != null)
                menuService.Get("_View").Add(new MenuItemViewModel("_Console", 1,
                                                                   new BitmapImage(
                                                                       new Uri(
                                                                           @"pack://application:,,,/Turtle-IDE.Core;component/Icons/Undo_16x.png")),
                                                                   manager.GetCommand("CONSOLESHOW"))
                { IsCheckable = true, IsChecked = console.IsVisible });

            menuService.Get("_View").Add(new MenuItemViewModel("Themes", 1));

            //Set the checkmark of the theme menu's based on which is currently selected
            menuService.Get("_View").Get("Themes").Add(new MenuItemViewModel("Dark", 1, null,
                                                                             manager.GetCommand("THEMECHANGE"))
            {
                IsCheckable = true,
                IsChecked = (themeSettings.SelectedTheme == "Dark"),
                CommandParameter = "Dark"
            });
            menuService.Get("_View").Get("Themes").Add(new MenuItemViewModel("Light", 2, null,
                                                                             manager.GetCommand("THEMECHANGE"))
            {
                IsCheckable = true,
                IsChecked = (themeSettings.SelectedTheme == "Light"),
                CommandParameter = "Light"
            });

            menuService.Add(new MenuItemViewModel("_Tools", 4));
            menuService.Get("_Tools").Add(new MenuItemViewModel("Settings", 1, null, settingsManager.SettingsCommand));

            menuService.Add(new MenuItemViewModel("_Help", 4));
        }

        #region Commands

        #region Open

        private void OpenModule()
        {
            var service = _container.Resolve<IOpenDocumentService>();
            service.Open();
        }

        #endregion

        #region Save

        private bool CanExecuteSaveDocument()
        {
            IWorkspace workspace = _container.Resolve<AbstractWorkspace>();
            if (workspace.ActiveDocument != null)
            {
                return workspace.ActiveDocument.Model.IsDirty;
            }
            return false;
        }

        private bool CanExecuteSaveAsDocument()
        {
            IWorkspace workspace = _container.Resolve<AbstractWorkspace>();
            return (workspace.ActiveDocument != null);
        }

        private void SaveDocument()
        {
            IWorkspace workspace = _container.Resolve<AbstractWorkspace>();
            ICommandManager manager = _container.Resolve<ICommandManager>();
            workspace.ActiveDocument.Handler.SaveContent(workspace.ActiveDocument);
            manager.Refresh();
        }

        private void SaveAsDocument()
        {
            IWorkspace workspace = _container.Resolve<AbstractWorkspace>();
            ICommandManager manager = _container.Resolve<ICommandManager>();
            if (workspace.ActiveDocument != null)
            {
                workspace.ActiveDocument.Handler.SaveContent(workspace.ActiveDocument, true);
                manager.Refresh();
            }
        }
        #endregion

        #region Theme

        private void ThemeChangeCommand(string s)
        {
            var manager = _container.Resolve<IThemeManager>();
            var menuService = _container.Resolve<IMenuService>();
            var win = _container.Resolve<IShell>() as Window;
            MenuItemViewModel mvm =
                menuService.Get("_View").Get("Themes").Get(manager.CurrentTheme.Name) as MenuItemViewModel;

            if (manager.CurrentTheme.Name != s)
            {
                if (mvm != null)
                    mvm.IsChecked = false;
                win.Dispatcher.InvokeAsync(() => manager.SetCurrent(s));
            }
            else
            {
                if (mvm != null)
                    mvm.IsChecked = true;
            }
        }

        #endregion

        #region Logger click

        private void ToggleLogger()
        {
            IWorkspace workspace = _container.Resolve<AbstractWorkspace>();
            var menuService = _container.Resolve<IMenuService>();
            ToolViewModel logger = workspace.Tools.First(f => f.ContentId == "Logger");
            if (logger != null)
            {
                logger.IsVisible = !logger.IsVisible;
                var mi = menuService.Get("_View").Get("_Logger") as MenuItemViewModel;
                mi.IsChecked = logger.IsVisible;
            }
        }

        #endregion

        #region Console click

        private void ToggleConsole()
        {
            IWorkspace workspace = _container.Resolve<AbstractWorkspace>();
            var menuService = _container.Resolve<IMenuService>();
            ToolViewModel console = workspace.Tools.First(f => f.ContentId == "Console");
            if (console != null)
            {
                console.IsVisible = !console.IsVisible;
                var mi = menuService.Get("_View").Get("_Console") as MenuItemViewModel;
                mi.IsChecked = console.IsVisible;
            }
        }

        #endregion

        #region Run Python Script

        private void runPython()
        {
            IWorkspace workspace = _container.Resolve<AbstractWorkspace>();
            var textViewModel = workspace.ActiveDocument as TextViewModel;
            string pyPath = Environment.CurrentDirectory + @"\External\WPy3710\python-3.7.1\python.exe";

            if (textViewModel == null) { return; }

            TextModel textModel = textViewModel.Model as TextModel;

            if (textModel == null) { return; }

            string location = textModel.Location as string;

            if (location == null)
            {
                //If there is no location, just prompt for Save As..
                string statementsToRun = textModel.Document.Text;

                DirectoryInfo info = new DirectoryInfo(Environment.CurrentDirectory + @"\temp");
                if (!info.Exists)
                {
                    info.Create();
                }
                FileStream fs = new FileStream(Environment.CurrentDirectory +
                    @"\temp\temp.py", FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs);
                sw.WriteLine(statementsToRun);
                sw.Close();
                fs.Close();

                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    var cmd = new Process();
                    cmd.StartInfo.FileName = pyPath;
                    cmd.StartInfo.Arguments = Environment.CurrentDirectory + @"\temp\temp.py";
                    //cmd.StartInfo.WorkingDirectory = pyFile.Replace("\\" + Path.GetFileName(pyFile), "");
                    //cmd.StartInfo.WindowStyle = windowStyle;
                    cmd.Start();
                    cmd.WaitForExit();
                }).Start();
            }
            else
            {
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    var cmd = new Process();
                    cmd.StartInfo.FileName = pyPath;
                    cmd.StartInfo.Arguments = location;
                    cmd.StartInfo.WorkingDirectory = location.Replace("\\" + Path.GetFileName(location), "");
                    //cmd.StartInfo.WindowStyle = windowStyle;
                    cmd.Start();
                    cmd.WaitForExit();
                }).Start();
            }
        }
        #endregion

        #endregion
    }
}
