using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Valve.VR;

namespace UniversalTrackerMarkers
{

    internal class OVRException : Exception {
        public OVRException(string message) : base(message) { }
    }

    public class DeviceListEntry
    {
        public DeviceListEntry(ETrackedDeviceClass type, uint handle, string serialNumber)
        {
            Type = type;
            Handle = handle;
            SerialNumber = serialNumber;
        }

        public ETrackedDeviceClass Type { get; }
        public uint Handle { get; }
        public string SerialNumber { get; }
    }

    internal class TrackedObjectListEntry
    {
        public uint Index { get; set; }
        public string Name { get; set; } = "";

    }

    internal class ExistingOverlay
    {
        public ulong HandleFront { get; set; }
        public ulong HandleBack { get; set; }
        public string TexturePath { get; set; } = string.Empty;
    }

    internal class OpenVRManager
    {
        private CVRSystem? _cVR;
        private CancellationTokenSource _cancelTokenSource = new CancellationTokenSource();
        private Thread? _thread = null;
        private Dictionary<string, DeviceListEntry> _devices = new Dictionary<string, DeviceListEntry>();
        private Dictionary<int, ExistingOverlay> _overlays = new Dictionary<int, ExistingOverlay>();

        public OpenVRManager()
        {

        }

        public void UpdateDevices()
        {
            if (_cVR == null) return;

            _devices.Clear();

            try
            {
                for (uint idx = 0; idx < OpenVR.k_unMaxTrackedDeviceCount; idx++)
                {
                    var deviceClass = OpenVR.System.GetTrackedDeviceClass(idx);

                    if (deviceClass == ETrackedDeviceClass.Invalid)
                    {
                        break;
                    }

                    var serialNumber = GetStringTrackedDeviceProperty(idx, ETrackedDeviceProperty.Prop_SerialNumber_String);

                    _devices.Add(serialNumber, new DeviceListEntry(deviceClass, idx, serialNumber));
                }
            }
            catch (OVRException e)
            {
                MessageBox.Show("Updating tracked devices encountered an unexpected OpenVR error: " + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public List<DeviceListEntry> GetAllDevices()
        {
            return _devices.Values.ToList();
        }

        public bool DeviceExists(string serialNumber)
        {
            return _devices.ContainsKey(serialNumber);
        }

        private string GetStringTrackedDeviceProperty(uint deviceIndex, ETrackedDeviceProperty prop)
        {
            ETrackedPropertyError err = ETrackedPropertyError.TrackedProp_Success;

            StringBuilder sb = new StringBuilder(128);
            uint propLen = OpenVR.System.GetStringTrackedDeviceProperty(deviceIndex, prop, sb, (uint)sb.Capacity, ref err);
            if (err == ETrackedPropertyError.TrackedProp_Success)
            {
                return sb.ToString();
            }
            else if (err == ETrackedPropertyError.TrackedProp_BufferTooSmall)
            {
                // try again with larger buffer
                sb.Capacity = (int)propLen;
                propLen = OpenVR.System.GetStringTrackedDeviceProperty(deviceIndex, prop, sb, (uint)sb.Capacity, ref err);
            }

            if (err != ETrackedPropertyError.TrackedProp_Success)
            {
                throw new OVRException(err.ToString());
            }

            return sb.ToString();
        }

        public bool InitOverlay()
        {
            if (_cVR != null)
            {
                Shutdown();
            }

            EVRInitError error = EVRInitError.None;
            _cVR = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Overlay);

            if (error != EVRInitError.None)
            {
                MessageBox.Show("Error while connecting to SteamVR: " + error.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            } 
            else
            {
                return true;
            }
        }

        public void Shutdown()
        {
            if (_cVR != null)
            {
                OpenVR.Shutdown();
                _cVR = null;
            }
        }

        public Matrix<float> GetRelativeTransformBetweenDevices(string sourceSN, string targetSN)
        {
            if (!_devices.ContainsKey(sourceSN))
            {
                throw new Exception($"No device with serial '{sourceSN}' exists");
            }
            if (!_devices.ContainsKey(targetSN))
            {
                throw new Exception($"No device with serial '{targetSN}' exists");
            }

            uint sourceIndex = _devices[sourceSN].Handle;
            uint targetIndex = _devices[targetSN].Handle;

            TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];

            OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseRawAndUncalibrated, 0, poses);

            var sourcePose = poses[sourceIndex];
            var sourceMatrix = MathUtils.OVR34ToMat44(ref sourcePose.mDeviceToAbsoluteTracking);

            var targetPose = poses[targetIndex];
            var targetMatrix = MathUtils.OVR34ToMat44(ref targetPose.mDeviceToAbsoluteTracking);

            // "subtract" the source from the target to get the transfrom from source to target
            var sourceInverse = sourceMatrix.Inverse();
            var sourceToTarget = sourceInverse * targetMatrix;

            return sourceToTarget;
        }

        public void UpdateOverlays(IEnumerable<MarkerConfiguration> markers)
        {
            try
            {
                foreach (var marker in markers)
                {
                    bool overlayExists = _overlays.ContainsKey(marker.Id);
                    bool shouldExist = marker.Enabled && marker.IsValid;

                    string fullTexturePath = string.Empty;
                    if (shouldExist) // run some more validations
                    {
                        fullTexturePath = Path.GetFullPath(marker.TexturePath!);
                        shouldExist &= File.Exists(fullTexturePath);

                        shouldExist &= _devices.ContainsKey(marker.TrackerSN!); // ensure the tracker SN is actually valid
                    }

                    if (!overlayExists && shouldExist)
                    {
                        ulong newHandleFront = OpenVR.k_ulOverlayHandleInvalid;
                        ThrowOVRError(OpenVR.Overlay.CreateOverlay($"com.jangxx.markers.id_front_{marker.Id}", $"Universal Tracker Marker {marker.Id} Front", ref newHandleFront));
                        ThrowOVRError(OpenVR.Overlay.SetOverlayFromFile(newHandleFront, fullTexturePath));
                        ThrowOVRError(OpenVR.Overlay.ShowOverlay(newHandleFront));

                        ulong newHandleBack = OpenVR.k_ulOverlayHandleInvalid;
                        ThrowOVRError(OpenVR.Overlay.CreateOverlay($"com.jangxx.markers.id_back_{marker.Id}", $"Universal Tracker Marker {marker.Id} Back", ref newHandleBack));
                        ThrowOVRError(OpenVR.Overlay.SetOverlayFromFile(newHandleBack, fullTexturePath));
                        ThrowOVRError(OpenVR.Overlay.ShowOverlay(newHandleBack));

                        _overlays.Add(marker.Id, new ExistingOverlay()
                        {
                            HandleFront = newHandleFront,
                            HandleBack = newHandleBack,
                            TexturePath = fullTexturePath,
                        });
                        overlayExists = true;
                    }
                    else if (overlayExists && !shouldExist)
                    {
                        ThrowOVRError(OpenVR.Overlay.DestroyOverlay(_overlays[marker.Id].HandleFront));
                        ThrowOVRError(OpenVR.Overlay.DestroyOverlay(_overlays[marker.Id].HandleBack));
                        _overlays.Remove(marker.Id);
                        overlayExists = false;
                    }

                    if (!overlayExists) continue;

                    var overlay = _overlays[marker.Id];

                    Matrix<float> overlayMatrixFront = MathUtils.createRotationMatrix44(
                        (float)(marker.RotateX / 180.0 * Math.PI),
                        (float)(marker.RotateY / 180.0 * Math.PI),
                        (float)(marker.RotateZ / 180.0 * Math.PI)
                    );
                    Matrix<float> overlayMatrixBack = MathUtils.createTransformMatrix44(
                        0,
                        (float)Math.PI, // add one 180deg rotation
                        0,
                        0, 0, 0,
                        -1, 1, 1 // mirror along the x axis
                    );

                    var translationMatrix = MathUtils.createTranslationMatrix44((float)marker.OffsetX, (float)marker.OffsetY, (float)marker.OffsetZ);

                    overlayMatrixBack = translationMatrix * overlayMatrixFront * overlayMatrixBack;
                    overlayMatrixFront = translationMatrix * overlayMatrixFront;

                    HmdMatrix34_t overlayMatrixOVR = new HmdMatrix34_t();

                    MathUtils.CopyMat34ToOVR(ref overlayMatrixFront, ref overlayMatrixOVR);
                    ThrowOVRError(OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(overlay.HandleFront, _devices[marker.TrackerSN!].Handle, ref overlayMatrixOVR));

                    MathUtils.CopyMat34ToOVR(ref overlayMatrixBack, ref overlayMatrixOVR);
                    ThrowOVRError(OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(overlay.HandleBack, _devices[marker.TrackerSN!].Handle, ref overlayMatrixOVR));

                    ThrowOVRError(OpenVR.Overlay.SetOverlayWidthInMeters(overlay.HandleFront, (float)marker.OverlayWidth));
                    ThrowOVRError(OpenVR.Overlay.SetOverlayWidthInMeters(overlay.HandleBack, (float)marker.OverlayWidth));

                    if (fullTexturePath != overlay.TexturePath)
                    {
                        ThrowOVRError(OpenVR.Overlay.SetOverlayFromFile(overlay.HandleFront, fullTexturePath));
                        ThrowOVRError(OpenVR.Overlay.SetOverlayFromFile(overlay.HandleBack, fullTexturePath));
                        overlay.TexturePath = fullTexturePath;
                    }
                }
            }
            catch (OVRException e)
            {
                MessageBox.Show("Unexpected OpenVR error: " + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception e)
            {
                MessageBox.Show("Unexpected error: " + e.ToString() + " (" + e.Message + ")", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ThrowOVRError(EVROverlayError err)
        {
            if (err != EVROverlayError.None)
            {
                throw new OVRException(err.ToString());
            }
        }

        private void ThrowOVRError(EVRCompositorError err)
        {
            if (err != EVRCompositorError.None)
            {
                throw new OVRException(err.ToString());
            }
        }

        public void StopThread()
        {
            if (_thread == null) return;

            _cancelTokenSource.Cancel();
            _thread.Join();
            _thread = null;
        }
    }
}