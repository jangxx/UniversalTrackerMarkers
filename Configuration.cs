using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UniversalTrackerMarkers
{
    public enum EProximityDevice
    {
        HMD = 0,
        LeftHand = 1,
        RightHand = 2,
        AnyHand = 3,
    }

    public class MarkerConfiguration : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private static int NextId = 1;

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MarkerConfiguration()
        {
            Id = NextId++;
        }

        [JsonIgnore]
        public int Id { get; }

        [JsonIgnore]
        public bool IsValid
        {
            get
            {
                return _trackerSN != null && _texturePath != null;
            }
        }

        private bool _enabled = true;
        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                RaisePropertyChanged(nameof(Enabled));
            }
        }

        private string _name = String.Empty;
        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                RaisePropertyChanged(nameof(Name));
            }
        }

        private string? _trackerSN = null;
        public string? TrackerSN
        {
            get { return _trackerSN; }
            set
            {
                _trackerSN = value;
                RaisePropertyChanged(nameof(TrackerSN));
            }
        }

        private string? _texturePath = null;
        public string? TexturePath
        {
            get { return _texturePath; }
            set
            {
                _texturePath = value;
                RaisePropertyChanged(nameof(TexturePath));
            }
        }

        private double _overlayOpacity = 1.0;
        public double OverlayOpacity
        {
            get { return _overlayOpacity; }
            set
            {
                _overlayOpacity = value;
                RaisePropertyChanged(nameof(OverlayOpacity));
            }
        }

        private double _overlayWidth = 1.0;
        public double OverlayWidth
        {
            get { return _overlayWidth; }
            set
            {
                _overlayWidth = value;
                RaisePropertyChanged(nameof(OverlayWidth));
            }
        }

        private double _offsetX = 0.0;
        public double OffsetX
        {
            get { return _offsetX; }
            set
            {
                _offsetX = value;
                RaisePropertyChanged(nameof(OffsetX));
            }
        }

        private double _offsetY = 0.0;
        public double OffsetY
        {
            get { return _offsetY; }
            set
            {
                _offsetY = value;
                RaisePropertyChanged(nameof(OffsetY));
            }
        }

        private double _offsetZ = 0.0;
        public double OffsetZ
        {
            get { return _offsetZ; }
            set
            {
                _offsetZ = value;
                RaisePropertyChanged(nameof(OffsetZ));
            }
        }

        private double _rotateX = 0.0;
        public double RotateX
        {
            get { return _rotateX; }
            set
            {
                _rotateX = value;
                RaisePropertyChanged(nameof(RotateX));
            }
        }

        private double _rotateY = 0.0;
        public double RotateY
        {
            get { return _rotateY; }
            set
            {
                _rotateY = value;
                RaisePropertyChanged(nameof(RotateY));
            }
        }

        private double _rotateZ = 0.0;
        public double RotateZ
        {
            get { return _rotateZ; }
            set
            {
                _rotateZ = value;
                RaisePropertyChanged(nameof(RotateZ));
            }
        }

        private bool _proximityFeaturesEnabled = false;
        public bool ProximityFeaturesEnabled
        {
            get { return _proximityFeaturesEnabled; }
            set
            {
                _proximityFeaturesEnabled = value;
                RaisePropertyChanged(nameof(ProximityFeaturesEnabled));
            }
        }

        private EProximityDevice _proximityDevice = EProximityDevice.HMD;
        public EProximityDevice ProximityDevice
        {
            get { return _proximityDevice; }
            set
            {
                _proximityDevice = value;
                RaisePropertyChanged(nameof(ProximityDevice));
            }
        }

        private double _proximityFadeDistMax = 2.0;
        public double ProximityFadeDistMax
        {
            get { return _proximityFadeDistMax; }
            set
            {
                _proximityFadeDistMax = value;
                RaisePropertyChanged(nameof(ProximityFadeDistMax));
            }
        }

        private double _proximityFadeDistMin = 1.0;
        public double ProximityFadeDistMin
        {
            get { return _proximityFadeDistMin; }
            set
            {
                _proximityFadeDistMin = value;
                RaisePropertyChanged(nameof(ProximityFadeDistMin));
            }
        }

        private bool _oscEnabled = false;
        public bool OscEnabled
        {
            get { return _oscEnabled; }
            set
            {
                _oscEnabled = value;
                RaisePropertyChanged(nameof(OscEnabled));
            }
        }

        private string? _oscAddress = null;
        public string? OscAddress
        {
            get { return _oscAddress; }
            set
            {
                _oscAddress = value;
                RaisePropertyChanged(nameof(OscAddress));
            }
        }

        private bool _oscStartHidden = false;
        public bool OscStartHidden
        {
            get { return _oscStartHidden; }
            set
            {
                _oscStartHidden = value;
                RaisePropertyChanged(nameof(OscStartHidden));
            }
        }
    }

    public class OscConfiguration : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool _enabled = false;
        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                RaisePropertyChanged(nameof(Enabled));
            }
        }

        private string _listenAddress = "127.0.0.1";
        public string ListenAddress
        {
            get { return _listenAddress; }
            set
            {
                _listenAddress = value;
                RaisePropertyChanged(nameof(ListenAddress));
            }
        }

        private int _listenPort = 37321;
        public int ListenPort
        {
            get { return _listenPort; }
            set
            {
                _listenPort = value;
                RaisePropertyChanged(nameof(ListenPort));
            }
        }
    }

    public class Configuration
    {
        //public List<MarkerConfiguration> Markers { get; set; } = new List<MarkerConfiguration>();
        public ObservableCollection<MarkerConfiguration> Markers { get; set; } = new ObservableCollection<MarkerConfiguration>();

        public OscConfiguration Osc { get; set; } = new OscConfiguration();
    }

    public class ConfigLoader
    {
        public static Configuration? LoadConfig(string path)
        {
            string jsonString = File.ReadAllText(path);

            if (jsonString == null) return null;

            return JsonSerializer.Deserialize<Configuration>(jsonString);
        }
    }
}
