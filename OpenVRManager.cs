using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Complex;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Valve.VR;
using Vortice.Direct3D11;
using static System.Net.Mime.MediaTypeNames;
using System.Reflection.Metadata;

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
        public ulong? SerialOverlayHandle { get; set; }
        public ID3D11Texture2D? SerialOverlayTexture { get; set; }
    }

    internal class TrackedObjectListEntry
    {
        public uint Index { get; set; }
        public string Name { get; set; } = "";

    }

    internal class ExistingOverlay
    {
        public uint DeviceIndex { get; set; }
        public ulong HandleFront { get; set; }
        public ulong HandleBack { get; set; }
        public string TexturePath { get; set; } = string.Empty;
        public EProximityDevice? ProximityDevice { get; set; }
        public double ProximityMin { get; set; }
        public double ProximityMax { get; set; }
        public double MaxOpacity { get; set; }
        public Vector<float> TranslationVec { get; set; } = Vector<float>.Build.Dense(3);
        public bool Visible { get; set; } = true;
    }

    internal class OpenVRManager
    {
        private DirectXManager _directXManager;

        private CVRSystem? _cVR;
        private CancellationTokenSource _cancelTokenSource = new CancellationTokenSource();
        private Thread? _thread = null;
        private Dictionary<string, DeviceListEntry> _devices = new Dictionary<string, DeviceListEntry>();
        private Dictionary<int, ExistingOverlay> _overlays = new Dictionary<int, ExistingOverlay>();
        private bool _serialNumbersShown = false;

        public OpenVRManager(DirectXManager directXManager)
        {
            _directXManager = directXManager;
        }

        public void UpdateDevices()
        {
            if (_cVR == null) return;

            lock (_devices)
            {
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

        public void UpdateMarkerVisility(int markerId, bool visible)
        {
            lock (_overlays)
            {
                if (!_overlays.ContainsKey(markerId)) return;

                Debug.WriteLine($"Setting visibiliy of marker {markerId} to {visible}");

                var overlay = _overlays[markerId];

                if (!visible)
                {
                    ThrowOVRError(OpenVR.Overlay.HideOverlay(overlay.HandleFront));
                    ThrowOVRError(OpenVR.Overlay.HideOverlay(overlay.HandleBack));
                }
                else
                {
                    ThrowOVRError(OpenVR.Overlay.ShowOverlay(overlay.HandleFront));
                    ThrowOVRError(OpenVR.Overlay.ShowOverlay(overlay.HandleBack));
                }
            }
        }

        private ID3D11Texture2D CreateSerialTextTexture(string serialNumber)
        {
            FormattedText text = new FormattedText(serialNumber,
                new CultureInfo("en-us"),
                FlowDirection.LeftToRight,
                new Typeface("Arial"),
                100,
                Brushes.White,
                1.0
            );
            text.TextAlignment = TextAlignment.Center;

            DrawingVisual drawingVisual = new DrawingVisual();
            DrawingContext drawingContext = drawingVisual.RenderOpen();

            drawingContext.DrawText(text, new Point(512, 256));
            drawingContext.Close();

            RenderTargetBitmap bmp = new RenderTargetBitmap(1024, 512, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(drawingVisual);

            return _directXManager.CreateTextureFromBitmap(bmp);
        }

        public void SetSerialNumbersShown(bool shown)
        {
            _serialNumbersShown = shown;

            lock (_devices)
            {
                if (_serialNumbersShown)
                {
                    foreach (var device in _devices.Values)
                    {
                        if (!device.SerialOverlayHandle.HasValue)
                        {
                            ulong handle = OpenVR.k_ulOverlayHandleInvalid;
                            ThrowOVRError(OpenVR.Overlay.CreateOverlay($"com.jangxx.markers.serial_{device.SerialNumber}", $"Universal Tracker Serial Overlay {device.SerialNumber}", ref handle));

                            var dxTexture = CreateSerialTextTexture(device.SerialNumber);
                            device.SerialOverlayTexture = dxTexture;

                            Texture_t ovrTexture = new Texture_t();
                            ovrTexture.eType = ETextureType.DirectX;
                            ovrTexture.handle = dxTexture.NativePointer;
                            ovrTexture.eColorSpace = EColorSpace.Auto;

                            ThrowOVRError(OpenVR.Overlay.SetOverlayTexture(handle, ref ovrTexture));

                            ThrowOVRError(OpenVR.Overlay.ShowOverlay(handle));
                            ThrowOVRError(OpenVR.Overlay.SetOverlayWidthInMeters(handle, 0.3f));

                            device.SerialOverlayHandle = handle;
                        }
                    }

                    _directXManager.Flush();
                }
                else
                {
                    foreach (var device in _devices.Values)
                    {
                        if (device.SerialOverlayHandle.HasValue)
                        {
                            device.SerialOverlayTexture?.Dispose();

                            ThrowOVRError(OpenVR.Overlay.DestroyOverlay(device.SerialOverlayHandle.Value));

                            device.SerialOverlayHandle = null;
                        }
                    }
                }
            }
        }

        public void UpdateOverlays(IEnumerable<MarkerConfiguration> markers)
        {
            try
            {
                lock (_overlays)
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

                            ulong newHandleBack = OpenVR.k_ulOverlayHandleInvalid;
                            ThrowOVRError(OpenVR.Overlay.CreateOverlay($"com.jangxx.markers.id_back_{marker.Id}", $"Universal Tracker Marker {marker.Id} Back", ref newHandleBack));
                            ThrowOVRError(OpenVR.Overlay.SetOverlayFromFile(newHandleBack, fullTexturePath));
                            

                            if (!(marker.OscEnabled && marker.OscStartHidden))
                            {
                                ThrowOVRError(OpenVR.Overlay.ShowOverlay(newHandleFront));
                                ThrowOVRError(OpenVR.Overlay.ShowOverlay(newHandleBack));
                            }

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

                        overlay.ProximityDevice = (marker.ProximityFeaturesEnabled) ? marker.ProximityDevice : null;
                        overlay.ProximityMin = marker.ProximityFadeDistMin;
                        overlay.ProximityMax = marker.ProximityFadeDistMax;
                        overlay.MaxOpacity = marker.OverlayOpacity;
                        overlay.TranslationVec = Vector<float>.Build.DenseOfArray(new float[]{
                            (float)marker.OffsetX,
                            (float)marker.OffsetY,
                            (float)marker.OffsetZ
                        });
                        overlay.DeviceIndex = _devices[marker.TrackerSN!].Handle;

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
                        ThrowOVRError(OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(overlay.HandleFront, overlay.DeviceIndex, ref overlayMatrixOVR));

                        MathUtils.CopyMat34ToOVR(ref overlayMatrixBack, ref overlayMatrixOVR);
                        ThrowOVRError(OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(overlay.HandleBack, overlay.DeviceIndex, ref overlayMatrixOVR));

                        ThrowOVRError(OpenVR.Overlay.SetOverlayWidthInMeters(overlay.HandleFront, (float)marker.OverlayWidth));
                        ThrowOVRError(OpenVR.Overlay.SetOverlayWidthInMeters(overlay.HandleBack, (float)marker.OverlayWidth));

                        if (fullTexturePath != overlay.TexturePath)
                        {
                            ThrowOVRError(OpenVR.Overlay.SetOverlayFromFile(overlay.HandleFront, fullTexturePath));
                            ThrowOVRError(OpenVR.Overlay.SetOverlayFromFile(overlay.HandleBack, fullTexturePath));
                            overlay.TexturePath = fullTexturePath;
                        }

                        // if the proximity devcice is not null, this opacity will be controller in the main thread instead
                        if (overlay.ProximityDevice == null)
                        {
                            ThrowOVRError(OpenVR.Overlay.SetOverlayAlpha(overlay.HandleFront, (float)overlay.MaxOpacity));
                            ThrowOVRError(OpenVR.Overlay.SetOverlayAlpha(overlay.HandleBack, (float)overlay.MaxOpacity));
                        }
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

        public void StartThread()
        {
            if (_thread != null) return;

            _cancelTokenSource.TryReset();

            _thread = new Thread(() => ThreadMain());
            _thread.Name = "OpenVRManagerThread";
            _thread.IsBackground = true;
            _thread.Start();
        }

        private double? GetPoseOverlayDistance(ExistingOverlay overlay, TrackedDevicePose_t devicePose, TrackedDevicePose_t overlayDevicePose)
        {
            if (devicePose.bPoseIsValid && overlayDevicePose.bPoseIsValid)
            {
                Vector<float> devicePos = MathUtils.extractTranslationFromOVR34(ref devicePose.mDeviceToAbsoluteTracking);
                Vector<float> overlayPos = MathUtils.OVR34ToMat44(ref overlayDevicePose.mDeviceToAbsoluteTracking) * Vector<float>.Build.DenseOfEnumerable(overlay.TranslationVec.Append(1));

                return (overlayPos.SubVector(0, 3) - devicePos).L2Norm();
            }
            else
            {
                return null;
            }
        }

        public void ThreadMain()
        {
            TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];

            while (true)
            {
                if (_cancelTokenSource.Token.WaitHandle.WaitOne(20)) // run at 50ish fps
                {
                    return; // cancellation was requested
                }

                OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseRawAndUncalibrated, 0, poses);

                var hmdPose = poses[0];

                var leftHandIdx = OpenVR.System.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
                var leftHandPose = (leftHandIdx < OpenVR.k_unMaxTrackedDeviceCount) ? poses[leftHandIdx] : new TrackedDevicePose_t();

                var rightHandIdx = OpenVR.System.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
                var rightHandPose = (rightHandIdx < OpenVR.k_unMaxTrackedDeviceCount) ? poses[rightHandIdx] : new TrackedDevicePose_t();

                lock (_overlays)
                {
                    foreach (var overlay in _overlays.Values)
                    {
                        if (overlay.ProximityDevice == null || !overlay.Visible)
                        {
                            continue;
                        }

                        TrackedDevicePose_t overlayDevicePose = poses[overlay.DeviceIndex];

                        double? distance = null;

                        switch (overlay.ProximityDevice)
                        {
                            case EProximityDevice.HMD:
                                distance = GetPoseOverlayDistance(overlay, hmdPose, overlayDevicePose);
                                break;
                            case EProximityDevice.LeftHand:
                                distance = GetPoseOverlayDistance(overlay, leftHandPose, overlayDevicePose);
                                break;
                            case EProximityDevice.RightHand:
                                distance = GetPoseOverlayDistance(overlay, rightHandPose, overlayDevicePose);
                                break;
                            case EProximityDevice.AnyHand:
                                double? distanceLeft = GetPoseOverlayDistance(overlay, leftHandPose, overlayDevicePose);
                                double? distanceRight = GetPoseOverlayDistance(overlay, rightHandPose, overlayDevicePose);

                                if (distanceLeft.HasValue && distanceRight.HasValue)
                                {
                                    distance = Math.Min(distanceLeft.Value, distanceRight.Value);
                                }
                                else if (distanceLeft.HasValue)
                                {
                                    distance = distanceLeft;
                                }
                                else
                                {
                                    distance = distanceRight;
                                }
                                break;
                            default:
                                continue;
                        }

                        if (distance.HasValue)
                        {
                            double targetOpacity = (1.0 - (distance.Value - overlay.ProximityMin) / (overlay.ProximityMax - overlay.ProximityMin)) * overlay.MaxOpacity;

                            ThrowOVRError(OpenVR.Overlay.SetOverlayAlpha(overlay.HandleFront, (float)targetOpacity));
                            ThrowOVRError(OpenVR.Overlay.SetOverlayAlpha(overlay.HandleBack, (float)targetOpacity));
                        }
                        else
                        {
                            // otherwise just set the configured max opacity
                            ThrowOVRError(OpenVR.Overlay.SetOverlayAlpha(overlay.HandleFront, (float)overlay.MaxOpacity));
                            ThrowOVRError(OpenVR.Overlay.SetOverlayAlpha(overlay.HandleBack, (float)overlay.MaxOpacity));
                        }
                    }
                }

                if (_serialNumbersShown)
                {
                    var hmdPosition = MathUtils.extractTranslationFromOVR34(ref hmdPose.mDeviceToAbsoluteTracking);

                    lock (_devices)
                    {
                        foreach (var device in _devices.Values)
                        {
                            if (!device.SerialOverlayHandle.HasValue) continue;

                            var devicePose = poses[device.Handle];

                            if (!devicePose.bPoseIsValid) continue;

                            var deviceRotationInverse = MathUtils.OVR34ToMat44(ref devicePose.mDeviceToAbsoluteTracking).SubMatrix(0, 3, 0, 3).Inverse();

                            var deviceRoationInverseFull = Matrix<float>.Build.DenseIdentity(4, 4);
                            deviceRoationInverseFull.SetSubMatrix(0, 0, deviceRotationInverse);

                            var devicePos = MathUtils.extractTranslationFromOVR34(ref devicePose.mDeviceToAbsoluteTracking);

                            devicePos[1] += (device.Handle == 0) ? 0.25f : 0.1f; // move the overlay a bit higher on the headset so it doesn't clip when looking up

                            var overlayMatrix = MathUtils.createTranslationMatrix44(devicePos) * MathUtils.LookAt44(hmdPosition, devicePos);

                            HmdMatrix34_t ovrMatrix = new HmdMatrix34_t();
                            MathUtils.CopyMat34ToOVR(ref overlayMatrix, ref ovrMatrix);                            

                            ThrowOVRError(OpenVR.Overlay.SetOverlayTransformAbsolute(device.SerialOverlayHandle.Value, ETrackingUniverseOrigin.TrackingUniverseRawAndUncalibrated, ref ovrMatrix));
                        }
                    }
                }
            }
        }
    }
}