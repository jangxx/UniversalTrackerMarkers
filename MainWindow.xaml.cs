using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Valve.VR;
using static UniversalTrackerMarkers.App;
using Path = System.IO.Path;

namespace UniversalTrackerMarkers
{
    public class DisplayDeviceListItem
    {
        public string Serial { get; set; }
        public bool Exists { get; set; }
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

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private Configuration _currentConfig = new Configuration();
        public Configuration CurrentConfig { get { return _currentConfig; } }

        private OpenVRManager _openVRManager = new OpenVRManager();
        private bool _hasUnsavedChanges = false;
        private bool _suppressInputEvents = false;

        public ObservableCollection<DisplayDeviceListItem> DeviceList { get; } = new ObservableCollection<DisplayDeviceListItem>();

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _currentConfig.Markers.CollectionChanged += HandleMarkersCollectionChanged;
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

            UpdateOverlays();
        }

        public bool Init()
        {
            bool initOVRResult = _openVRManager.InitOverlay();

            if (!initOVRResult) return false;

            UpdateControllersAndTrackers();
            return true;
        }

        private void UpdateControllersAndTrackers()
        {
            _openVRManager.UpdateDevices();

            DeviceList.Clear();

            foreach (var device in _openVRManager.GetAllDevices())
            {
                DeviceList.Add(new DisplayDeviceListItem(device, true));
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

            _currentConfig.Markers.CollectionChanged += HandleMarkersCollectionChanged;
            foreach (var marker in _currentConfig.Markers)
            {
                marker.PropertyChanged -= HandleMarkerPropertyChanged;
                marker.PropertyChanged += HandleMarkerPropertyChanged;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentConfig)));

            foreach(var marker in _currentConfig.Markers)
            {
                if (marker.TrackerSN != null && !_openVRManager.DeviceExists(marker.TrackerSN))
                {
                    DeviceList.Add(new DisplayDeviceListItem(marker.TrackerSN, false));
                }
            }

            _hasUnsavedChanges = false;

            UpdateOverlays();
        }

        private void SaveConfig(string path)
        {
            try
            {
                Debug.WriteLine("Saving config in " + path);

                string jsonString = JsonSerializer.Serialize(_currentConfig, new JsonSerializerOptions() { WriteIndented = true });
                File.WriteAllText(path, jsonString);

                _hasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while saving config: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateMarker()
        {
            var marker = new MarkerConfiguration();
            marker.Name = "New Marker";

            _currentConfig.Markers.Add(marker);

            //UpdateDisplayProperties();
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
    }
}