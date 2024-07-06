using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using Valve.VR;
using static UniversalTrackerMarkers.App;
using Path = System.IO.Path;

namespace UniversalTrackerMarkers
{
    public class DisplayDeviceListItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _serial;
        public string Serial {
            get { return _serial; }
            set
            {
                _serial = value;
                RaisePropertyChanged(nameof(Serial));
                RaisePropertyChanged(nameof(DisplayName));
            }
        }

        private bool _exists;
        public bool Exists {
            get { return _exists; }
            set
            {
                _exists = value;
                RaisePropertyChanged(nameof(Exists));
                RaisePropertyChanged(nameof(DisplayName));
            }
        }

        public ETrackedDeviceClass Type { get; set; }

        public string DisplayName
        {
            get { 
                if (Exists)
                {
                    return Serial + " (" + Enum.GetName(Type) + ")";
                }
                else
                {
                    return Serial + " (Not found)";
                } 
            }
        }

        public DisplayDeviceListItem(DeviceListEntry entry, bool exists)
        {
            Serial = entry.SerialNumber;
            Type = entry.Type;
            Exists = exists;
        }

        public DisplayDeviceListItem(string serialNumber, bool exists)
        {
            Serial = serialNumber;
            Type = ETrackedDeviceClass.Invalid;
            Exists = exists;
        }
    }

    public class OscDisplayState
    {
        public bool ServerRunning { get; set; } = false;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private Configuration _currentConfig = new Configuration();
        public Configuration CurrentConfig { get { return _currentConfig; } }

        private OscDisplayState _oscState = new OscDisplayState();
        public OscDisplayState OscState {  get { return _oscState; } }

        private bool _showSerialOnDevices = false;
        public bool ShowSerialOnDevices
        {
            get { return _showSerialOnDevices; }
            set
            {
                _showSerialOnDevices = value;
                _openVRManager.SetSerialNumbersShown(_showSerialOnDevices);
                RaisePropertyChanged(nameof(ShowSerialOnDevices));
            }
        }

        private DirectXManager _directXManager = new DirectXManager();
        private OpenVRManager _openVRManager;
        private OscListener? _oscListener;

        private bool _hasUnsavedChanges = false;
        private bool _suppressInputEvents = false;

        public ObservableCollection<DisplayDeviceListItem> DeviceList { get; } = new ObservableCollection<DisplayDeviceListItem>();

        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindow()
        {
            _openVRManager = new OpenVRManager(_directXManager);

            InitializeComponent();
            DataContext = this;

            SetupPropertyEventListeners();
        }

        public bool Init()
        {
            bool initOVRResult = _openVRManager.InitOverlay();

            if (!initOVRResult) return false;

            UpdateControllersAndTrackers();

            _openVRManager.ShutdownRequested += HandleOVRShutdownRequested;
            _openVRManager.DevicesChanged += HandleOVRDevicesChanged;

            _openVRManager.StartThread();
            return true;
        }

        public void ProcessStartupConfig()
        {
            if (_currentConfig.Settings.StartMinimized)
            {
                if (_currentConfig.Settings.MinimizeToTray)
                {
                    TrayIcon.Visibility = Visibility.Visible;
                    return;
                }

                WindowState = WindowState.Minimized;
            }

            Show();
        }

        private void UpdateControllersAndTrackers()
        {
            _openVRManager.UpdateDevices();

            foreach (var device in _openVRManager.GetAllDevices())
            {
                var newListItem = new DisplayDeviceListItem(device, true);

                var existingItem = DeviceList.FirstOrDefault(d => d.Serial == newListItem.Serial);

                if (existingItem == null)
                {
                    DeviceList.Add(newListItem);
                }
                else
                {
                    existingItem.Type = newListItem.Type;
                    existingItem.Exists = newListItem.Exists;
                }
            }
        }

        private void UpdateOverlays()
        {
            Debug.WriteLine("Overlays changed");
            _openVRManager.UpdateOverlays(CurrentConfig.Markers);
        }

        public void LoadConfig(string path)
        {
            Configuration? config;

            try
            {
                config = ConfigLoader.LoadConfig(path);
            }
            catch (JsonException ex)
            {
                MessageBox.Show("Could not parse config file: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (config == null) return;

            _currentConfig = config;

            SetupPropertyEventListeners();

            RaisePropertyChanged(nameof(CurrentConfig));

            foreach(var marker in _currentConfig.Markers)
            {
                if (marker.TrackerSN != null && !_openVRManager.DeviceExists(marker.TrackerSN))
                {
                    DeviceList.Add(new DisplayDeviceListItem(marker.TrackerSN, false));
                }
            }

            _hasUnsavedChanges = false;

            UpdateOverlays();
            UpdateOscListener();
        }

        private void SaveConfig(string path)
        {
            try
            {
                Debug.WriteLine("Saving config in " + path);

                ConfigLoader.WriteConfig(path, _currentConfig);

                _hasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while saving config: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupPropertyEventListeners()
        {
            _currentConfig.Markers.CollectionChanged += HandleMarkersCollectionChanged;

            foreach (var marker in _currentConfig.Markers)
            {
                marker.PropertyChanged -= HandleMarkerPropertyChanged;
                marker.PropertyChanged += HandleMarkerPropertyChanged;
            }

            _currentConfig.Osc.PropertyChanged += HandleOscConfigChanged;
            _currentConfig.Settings.PropertyChanged += HandleGenericConfigChanged;
        }

        private void HandleGenericConfigChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_suppressInputEvents)
            {
                _hasUnsavedChanges = true;
            }
        }

        private void HandleOscConfigChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_suppressInputEvents)
            {
                _hasUnsavedChanges = true;
            }

            UpdateOscListener();
        }

        private void UpdateOscListener()
        {
            if (_oscListener != null)
            {
                _oscListener.Stop();
                _oscListener = null;

                _oscState.ServerRunning = false;
                _oscState.ErrorMessage = string.Empty;
                RaisePropertyChanged(nameof(OscState));
            }

            OscConfiguration oscConfig = _currentConfig.Osc;

            if (oscConfig.Enabled && oscConfig.ListenAddress.Length > 0)
            {
                _oscListener = new OscListener();

                try
                {
                    _oscListener.OscListenerCrashed += HandleOscListenerCrashed;
                    _oscListener.OscBooleanMessageReceived += HandleOscBooleanMessageReceived;

                    _oscListener.Start(oscConfig.ListenAddress, oscConfig.ListenPort);

                    _oscState.ServerRunning = true;
                    _oscState.ErrorMessage = string.Empty;
                    RaisePropertyChanged(nameof(OscState));
                }
                catch (Exception ex)
                {
                    _oscState.ServerRunning = false;
                    _oscState.ErrorMessage = ex.Message;
                    RaisePropertyChanged(nameof(OscState));

                    _oscListener = null;
                }
            }
        }

        private void HandleMarkersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressInputEvents)
            {
                _hasUnsavedChanges = true;
            }

            foreach (var marker in _currentConfig.Markers)
            {
                marker.PropertyChanged -= HandleMarkerPropertyChanged;
                marker.PropertyChanged += HandleMarkerPropertyChanged;
            }

            UpdateOverlays();
        }

        private void HandleMarkerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_suppressInputEvents)
            {
                _hasUnsavedChanges = true;
            }

            if (sender != null)
            {
                var marker = (MarkerConfiguration)sender;

                if (e.PropertyName == nameof(marker.ProximityFadeDistMin) && marker.ProximityFadeDistMax < marker.ProximityFadeDistMin)
                {
                    marker.ProximityFadeDistMax = marker.ProximityFadeDistMin;
                }
                else if (e.PropertyName == nameof(marker.ProximityFadeDistMax) && marker.ProximityFadeDistMax < marker.ProximityFadeDistMin)
                {
                    marker.ProximityFadeDistMin = marker.ProximityFadeDistMax;
                }
            }

            UpdateOverlays();
        }


        private void CreateMarker()
        {
            var marker = new MarkerConfiguration();
            marker.Name = "New Marker";

            _currentConfig.Markers.Add(marker);
        }

        private void HandleCreateMarkerButton(object sender, RoutedEventArgs e)
        {
            CreateMarker();
        }

        private void HandleRefreshDevicesButton(object sender, RoutedEventArgs e)
        {
            UpdateControllersAndTrackers();
        }

        private void HandleOpenTextureButton(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image file|*.png;*.jpg;*.tga";
            openFileDialog.InitialDirectory = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "markers");

            if (openFileDialog.ShowDialog() == true)
            {
                MarkerConfiguration? selectedConfig = (MarkerConfiguration)MarkerList.SelectedItem;

                if (selectedConfig != null)
                {
                    selectedConfig.TexturePath = openFileDialog.FileName;
                }
            }
        }

        private void HandleResetTransformButton(object sender, RoutedEventArgs e)
        {
            MarkerConfiguration? selectedConfig = (MarkerConfiguration)MarkerList.SelectedItem;

            if (selectedConfig != null)
            {
                selectedConfig.OffsetX = 0;
                selectedConfig.OffsetY = 0;
                selectedConfig.OffsetZ = 0;
                selectedConfig.RotateX = 0;
                selectedConfig.RotateY = 0;
                selectedConfig.RotateZ = 0;
            }
        }

        private void SetInputValues(Action fn)
        {
            _suppressInputEvents = true;

            try
            {
                fn();
            }
            finally
            {
                _suppressInputEvents = false;
            }
        }

        private void HandleMainWindowClosing(object sender, CancelEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have some unsaved changes. Are you sure you want to exit?",
                    "Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }
        }

        private void HandleSaveConfigButton(object sender, RoutedEventArgs e)
        {
            var defaultConfigFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Const.DefaultConfigPath
            );

            Directory.CreateDirectory(defaultConfigFilePath);
            var saveFilePath = Path.Combine(defaultConfigFilePath, Const.DefaultConfigName);

            SaveConfig(saveFilePath);
        }

        private void HandleDeleteMarkerClick(object sender, RoutedEventArgs e)
        {
            MarkerConfiguration? selectedConfig = (MarkerConfiguration)MarkerList.SelectedItem;

            if (selectedConfig != null)
            {
                _currentConfig.Markers.Remove(selectedConfig);
            }
        }

        private void HandleDuplicateMarkerClick(object sender, RoutedEventArgs e)
        {
            MarkerConfiguration? selectedConfig = (MarkerConfiguration)MarkerList.SelectedItem;

            if (selectedConfig != null)
            {
                var clone = new MarkerConfiguration(selectedConfig);
                _currentConfig.Markers.Add(clone);
            }
        }

        private void HandleSetTransformRelativeButton(object sender, RoutedEventArgs e)
        {
            MarkerConfiguration? selectedConfig = (MarkerConfiguration)MarkerList.SelectedItem;
            DisplayDeviceListItem? selectedDevice = (DisplayDeviceListItem)RelativeDeviceSelect.SelectedItem;

            if (selectedConfig != null && selectedConfig.TrackerSN != null && selectedDevice != null)
            {
                var relativeTransform = _openVRManager.GetRelativeTransformBetweenDevices(selectedConfig.TrackerSN, selectedDevice.Serial);

                var relativeTranslate = MathUtils.extractTranslationFromMatrix44(relativeTransform);
                var relativeRotation = MathUtils.extractRotationsFromMatrix(relativeTransform.SubMatrix(0, 3, 0, 3));

                selectedConfig.OffsetX = relativeTranslate[0];
                selectedConfig.OffsetY = relativeTranslate[1];
                selectedConfig.OffsetZ = relativeTranslate[2];

                selectedConfig.RotateX = relativeRotation[0] / Math.PI * 180;
                selectedConfig.RotateY = relativeRotation[1] / Math.PI * 180;
                selectedConfig.RotateZ = relativeRotation[2] / Math.PI * 180;
            }
        }

        private void HandleOscBooleanMessageReceived(object? sender, EventArgs args)
        {
            var oscMessageReceivedArgs = (OscBooleanMessageReceivedArgs)args;

            Debug.WriteLine("Received bool from " + oscMessageReceivedArgs.Address + " value:" + oscMessageReceivedArgs.Value);

            var dispatcher = Application.Current.Dispatcher;
            dispatcher.BeginInvoke(new Action(() =>
            {
                foreach(var marker in _currentConfig.Markers)
                {
                    if (marker.OscEnabled && marker.OscAddress == oscMessageReceivedArgs.Address)
                    {
                        _openVRManager.UpdateMarkerVisility(marker.Id, oscMessageReceivedArgs.Value);
                    }
                }
            }));
        }

        private void HandleOscListenerCrashed(object? sender, EventArgs args)
        {
            var oscListenerCrashedArgs = (OscListenerCrashedArgs)args;

            var dispatcher = Application.Current.Dispatcher;
            dispatcher.BeginInvoke(new Action(() =>
            {
                _oscState.ServerRunning = false;
                _oscState.ErrorMessage = oscListenerCrashedArgs.Message;
                RaisePropertyChanged(nameof(OscState));

                _oscListener?.Stop();
                _oscListener = null;
            }));
        }

        private void HandleTrayClicked(object sender, RoutedEventArgs e)
        {
            TrayIcon.Visibility = Visibility.Collapsed;

            Show();
            WindowState = WindowState.Normal;
        }

        private void HandleWindowStateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                if (_currentConfig.Settings.MinimizeToTray)
                {
                    TrayIcon.Visibility = Visibility.Visible;
                    Hide();
                }
            }
        }

        private void HandleOVRDevicesChanged(object? sender, EventArgs e)
        {
            var dispatcher = Application.Current.Dispatcher;
            dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateControllersAndTrackers();
                UpdateOverlays();
            }));
        }

        private void HandleOVRShutdownRequested(object? sender, EventArgs e)
        {
            var dispatcher = Application.Current.Dispatcher;
            dispatcher.BeginInvoke(new Action(() =>
            {
                _openVRManager?.Shutdown();
                Application.Current.Shutdown();
            }));
        }
    }
}