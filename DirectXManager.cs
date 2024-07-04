using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Vortice;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Direct3D11.Debug;
using Vortice.DXGI;
using Vortice.DXGI.Debug;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;

namespace UniversalTrackerMarkers
{
    internal class DirectXManager
    {
        private static readonly FeatureLevel[] FEATURE_LEVELS = new[]
        {
            //FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0,
            //FeatureLevel.Level_10_1,
            //FeatureLevel.Level_10_0
        };

        private ID3D11Device1 _device;
        private ID3D11DeviceContext1 _deviceContext;
        private FeatureLevel _featureLevel;

        public DirectXManager()
        {
            var result = D3D11CreateDevice(
                IntPtr.Zero,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                FEATURE_LEVELS,
                out ID3D11Device tempDevice,
                out _featureLevel,
                out ID3D11DeviceContext tempContext
            );

            Debug.WriteLine("DX CreateDevice result: success=" + result.Success + " failure=" + result.Failure);

            if (result.Failure)
            {
                // fall back to WARP device (software rasterizer)
                D3D11CreateDevice(
                    IntPtr.Zero,
                    DriverType.Warp,
                    0,
                    FEATURE_LEVELS,
                    out tempDevice, out _featureLevel, out tempContext
                );
            }

            _device = tempDevice.QueryInterface<ID3D11Device1>();
            _deviceContext = tempContext.QueryInterface<ID3D11DeviceContext1>();

            tempDevice.Dispose();
            tempContext.Dispose();
        }

        public ID3D11Texture2D CreateTextureFromBitmap(RenderTargetBitmap bitmap)
        {
            Texture2DDescription textureDescription = new Texture2DDescription();
            textureDescription.Format = Format.B8G8R8A8_UNorm;
            textureDescription.Width = (int)bitmap.Width;
            textureDescription.Height = (int)bitmap.Height;
            textureDescription.ArraySize = 1;
            textureDescription.MipLevels = 1;
            textureDescription.CPUAccessFlags = CpuAccessFlags.None;
            textureDescription.BindFlags = BindFlags.ShaderResource;
            textureDescription.Usage = ResourceUsage.Immutable;
            textureDescription.SampleDescription.Count = 1;
            textureDescription.SampleDescription.Quality = 0;


            var pixelData = new byte[bitmap.PixelHeight * bitmap.PixelWidth * 4];

            bitmap.CopyPixels(pixelData, bitmap.PixelWidth * 4, 0);

            unsafe
            {
                fixed (byte* pixelPtr = pixelData)
                {
                    var initialData = new SubresourceData((IntPtr)pixelPtr, bitmap.PixelWidth * 4);

                    ID3D11Texture2D tex = _device.CreateTexture2D(textureDescription, initialData);

                    return tex;
                }
            }
        }

        public void Flush()
        {
            _deviceContext.Flush();
        }
    }
}
