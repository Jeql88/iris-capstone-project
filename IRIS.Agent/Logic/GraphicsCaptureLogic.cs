using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Serilog;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace IRIS.Agent.Logic
{
    // Final-fallback screen capture using Windows.Graphics.Capture (WinRT).
    // Used only when both CopyFromScreen and PrintWindow fail (Win11 24H2
    // GDI/DWM regression cases). Compositor-based, session-aware, bypasses
    // the legacy GDI session-DC surfaces entirely.
    internal sealed class GraphicsCaptureLogic : IDisposable
    {
        private IDirect3DDevice? _device;
        private IntPtr _d3dDevicePtr;
        private IntPtr _d3dContextPtr;

        // IGraphicsCaptureItemInterop — used to create a GraphicsCaptureItem
        // from an HMONITOR. Defined by WinRT but has no projection in C#,
        // so we call it via COM directly.
        [ComImport]
        [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IGraphicsCaptureItemInterop
        {
            IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
            IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
        }

        // RoGetActivationFactory takes an HSTRING, not a wide string. .NET's
        // P/Invoke marshaller rejects [MarshalAs(UnmanagedType.HString)] on
        // a string parameter (MarshalDirectiveException), so we construct
        // the HSTRING manually via WindowsCreateString and pass the handle.
        [DllImport("combase.dll", ExactSpelling = true, PreserveSig = true)]
        private static extern int RoGetActivationFactory(
            IntPtr activatableClassId,
            [In] ref Guid iid,
            out IntPtr factory);

        [DllImport("combase.dll", ExactSpelling = true, PreserveSig = true)]
        private static extern int WindowsCreateString(
            [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
            int length,
            out IntPtr hstring);

        [DllImport("combase.dll", ExactSpelling = true, PreserveSig = true)]
        private static extern int WindowsDeleteString(IntPtr hstring);

        public bool Initialize()
        {
            try
            {
                if (!GraphicsCaptureSession.IsSupported())
                {
                    Log.Warning("GraphicsCaptureSession.IsSupported() returned false; skipping WinRT capture path.");
                    return false;
                }

                var hr = NativeMethods.D3D11CreateDevice(
                    IntPtr.Zero,
                    NativeMethods.D3D_DRIVER_TYPE_HARDWARE,
                    IntPtr.Zero,
                    NativeMethods.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                    IntPtr.Zero,
                    0,
                    NativeMethods.D3D11_SDK_VERSION,
                    out _d3dDevicePtr,
                    out _,
                    out _d3dContextPtr);

                if (hr < 0 || _d3dDevicePtr == IntPtr.Zero)
                {
                    // Try WARP (software) as a last resort — rarely helps on lab
                    // PCs with real GPUs, but harmless to attempt.
                    hr = NativeMethods.D3D11CreateDevice(
                        IntPtr.Zero,
                        NativeMethods.D3D_DRIVER_TYPE_WARP,
                        IntPtr.Zero,
                        NativeMethods.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                        IntPtr.Zero,
                        0,
                        NativeMethods.D3D11_SDK_VERSION,
                        out _d3dDevicePtr,
                        out _,
                        out _d3dContextPtr);
                    if (hr < 0 || _d3dDevicePtr == IntPtr.Zero)
                    {
                        Log.Error("D3D11CreateDevice failed with HRESULT 0x{Hr:X8}", hr);
                        return false;
                    }
                }

                // IDXGIDevice
                var iidDxgiDevice = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
                var dxgiHr = Marshal.QueryInterface(_d3dDevicePtr, in iidDxgiDevice, out var dxgiDevicePtr);
                if (dxgiHr < 0 || dxgiDevicePtr == IntPtr.Zero)
                {
                    Log.Error("Failed to QueryInterface D3D11 -> IDXGIDevice (HRESULT 0x{Hr:X8})", dxgiHr);
                    return false;
                }

                try
                {
                    var wrapHr = NativeMethods.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevicePtr, out var inspectablePtr);
                    if (wrapHr < 0 || inspectablePtr == IntPtr.Zero)
                    {
                        Log.Error("CreateDirect3D11DeviceFromDXGIDevice failed (HRESULT 0x{Hr:X8})", wrapHr);
                        return false;
                    }

                    try
                    {
                        _device = MarshalInspectable<IDirect3DDevice>.FromAbi(inspectablePtr);
                    }
                    finally
                    {
                        Marshal.Release(inspectablePtr);
                    }
                }
                finally
                {
                    Marshal.Release(dxgiDevicePtr);
                }

                Log.Information("Graphics Capture (WinRT) path initialized.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize Graphics Capture path.");
                return false;
            }
        }

        public byte[]? CaptureFrame()
        {
            if (_device == null)
            {
                return null;
            }

            GraphicsCaptureItem? item;
            try
            {
                var hmonitor = NativeMethods.MonitorFromWindow(
                    NativeMethods.GetDesktopWindow(), NativeMethods.MONITOR_DEFAULTTOPRIMARY);
                if (hmonitor == IntPtr.Zero)
                {
                    Log.Warning("MonitorFromWindow returned NULL; no primary monitor for Graphics Capture.");
                    return null;
                }

                // Fetch the activation factory for GraphicsCaptureItem and
                // query IGraphicsCaptureItemInterop directly. RoGetActivationFactory
                // is the supported path on all CsWinRT versions.
                var classId = "Windows.Graphics.Capture.GraphicsCaptureItem";
                var interopIid = typeof(IGraphicsCaptureItemInterop).GUID;

                var hsHr = WindowsCreateString(classId, classId.Length, out var hstring);
                if (hsHr < 0 || hstring == IntPtr.Zero)
                {
                    Log.Error("WindowsCreateString for {Class} failed (HRESULT 0x{Hr:X8}).", classId, hsHr);
                    return null;
                }

                IntPtr factoryPtr;
                int roHr;
                try
                {
                    roHr = RoGetActivationFactory(hstring, ref interopIid, out factoryPtr);
                }
                finally
                {
                    WindowsDeleteString(hstring);
                }

                if (roHr < 0 || factoryPtr == IntPtr.Zero)
                {
                    Log.Error("RoGetActivationFactory for {Class} failed (HRESULT 0x{Hr:X8}).", classId, roHr);
                    return null;
                }

                IGraphicsCaptureItemInterop interop;
                try
                {
                    interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
                }
                finally
                {
                    Marshal.Release(factoryPtr);
                }

                var iid = GuidGenerator.CreateIID(typeof(GraphicsCaptureItem));
                var itemPtr = interop.CreateForMonitor(hmonitor, ref iid);
                if (itemPtr == IntPtr.Zero)
                {
                    Log.Warning("IGraphicsCaptureItemInterop.CreateForMonitor returned NULL.");
                    return null;
                }

                try
                {
                    item = MarshalInspectable<GraphicsCaptureItem>.FromAbi(itemPtr);
                }
                finally
                {
                    Marshal.Release(itemPtr);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Graphics Capture: failed to create GraphicsCaptureItem for primary monitor.");
                return null;
            }

            var size = item.Size;
            if (size.Width <= 0 || size.Height <= 0)
            {
                Log.Warning("Graphics Capture item reports non-positive size {W}x{H}.", size.Width, size.Height);
                return null;
            }

            using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                size);
            using var session = framePool.CreateCaptureSession(item);

            try { session.IsCursorCaptureEnabled = true; } catch { /* older builds */ }

            var gate = new ManualResetEventSlim(false);
            Direct3D11CaptureFrame? captured = null;
            framePool.FrameArrived += (s, _) =>
            {
                try
                {
                    captured ??= s.TryGetNextFrame();
                }
                catch { /* swallow, gate will time out */ }
                finally
                {
                    gate.Set();
                }
            };

            session.StartCapture();

            // Cap the wait tightly. If we can't get a frame in 1.5s, we'd
            // blow past the UI's per-request budget; better to fail fast
            // and let the placeholder serve.
            if (!gate.Wait(TimeSpan.FromMilliseconds(1500)) || captured == null)
            {
                Log.Warning("Graphics Capture: first frame did not arrive within 1500 ms.");
                return null;
            }

            try
            {
                return EncodeSurfaceToJpeg(captured.Surface, size.Width, size.Height);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Graphics Capture: failed to encode captured surface.");
                return null;
            }
            finally
            {
                captured.Dispose();
            }
        }

        // IDirect3DDxgiInterfaceAccess — fetches the underlying ID3D11Texture2D
        // from the WinRT IDirect3DSurface. Standard WinRT<->D3D11 interop shape.
        [ComImport]
        [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDirect3DDxgiInterfaceAccess
        {
            IntPtr GetInterface([In] ref Guid iid);
        }

        private byte[] EncodeSurfaceToJpeg(IDirect3DSurface surface, int width, int height)
        {
            // Pull the ID3D11Texture2D out of the WinRT surface so we can
            // copy its contents into a managed bitmap via a CPU-readable
            // staging texture.
            var access = (IDirect3DDxgiInterfaceAccess)(object)surface;
            var iidTex2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
            var texPtr = access.GetInterface(ref iidTex2D);
            if (texPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("IDirect3DDxgiInterfaceAccess returned NULL texture pointer.");
            }

            try
            {
                return CopyTextureToJpeg(texPtr, width, height);
            }
            finally
            {
                Marshal.Release(texPtr);
            }
        }

        // We deliberately keep the D3D11 staging copy path compact and
        // dependency-free: no SharpDX, no Vortice, just vtable calls on
        // the ID3D11Device / ID3D11DeviceContext / ID3D11Texture2D COM
        // objects we already have pointers to. This keeps the WinRT
        // addition cost small and avoids NuGet surface.
        //
        // Caller owns texPtr (does not take ownership).
        private byte[] CopyTextureToJpeg(IntPtr texPtr, int width, int height)
        {
            // Read the source texture description, then create a staging
            // texture with identical format + dimensions but CPU read access
            // and STAGING usage. CopyResource source -> staging. Map(READ).
            // Copy rows into a managed Bitmap (accounting for row pitch,
            // which may differ from width * 4). Encode JPEG via existing
            // System.Drawing path. Unmap + release.

            // ID3D11Texture2D::GetDesc (vtable index 10)
            var descSize = Marshal.SizeOf<D3D11_TEXTURE2D_DESC>();
            var descPtr = Marshal.AllocHGlobal(descSize);
            try
            {
                InvokeVTable<GetDescDelegate>(texPtr, 10, getDesc => getDesc(texPtr, descPtr));
                var desc = Marshal.PtrToStructure<D3D11_TEXTURE2D_DESC>(descPtr);

                desc.Usage = 3;          // D3D11_USAGE_STAGING
                desc.BindFlags = 0;
                desc.CPUAccessFlags = 0x20000; // D3D11_CPU_ACCESS_READ
                desc.MiscFlags = 0;

                Marshal.StructureToPtr(desc, descPtr, fDeleteOld: true);

                // ID3D11Device::CreateTexture2D (vtable index 5)
                IntPtr stagingPtr = IntPtr.Zero;
                InvokeVTable<CreateTexture2DDelegate>(_d3dDevicePtr, 5, createTex =>
                {
                    var hr = createTex(_d3dDevicePtr, descPtr, IntPtr.Zero, out stagingPtr);
                    if (hr < 0 || stagingPtr == IntPtr.Zero)
                    {
                        throw new InvalidOperationException($"ID3D11Device::CreateTexture2D failed (HRESULT 0x{hr:X8})");
                    }
                });

                try
                {
                    // ID3D11DeviceContext::CopyResource (vtable index 47)
                    InvokeVTable<CopyResourceDelegate>(_d3dContextPtr, 47, copy =>
                        copy(_d3dContextPtr, stagingPtr, texPtr));

                    // ID3D11DeviceContext::Map (vtable index 14)
                    var mapped = new D3D11_MAPPED_SUBRESOURCE();
                    InvokeVTable<MapDelegate>(_d3dContextPtr, 14, map =>
                    {
                        var hr = map(_d3dContextPtr, stagingPtr, 0, 1 /*D3D11_MAP_READ*/, 0, out mapped);
                        if (hr < 0)
                        {
                            throw new InvalidOperationException($"ID3D11DeviceContext::Map failed (HRESULT 0x{hr:X8})");
                        }
                    });

                    try
                    {
                        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                        var bmpData = bmp.LockBits(
                            new Rectangle(0, 0, width, height),
                            ImageLockMode.WriteOnly,
                            PixelFormat.Format32bppArgb);
                        try
                        {
                            unsafe
                            {
                                var srcRowBytes = width * 4;
                                for (int y = 0; y < height; y++)
                                {
                                    var src = (byte*)(mapped.pData + y * (int)mapped.RowPitch);
                                    var dst = (byte*)(bmpData.Scan0 + y * bmpData.Stride);
                                    Buffer.MemoryCopy(src, dst, srcRowBytes, srcRowBytes);
                                }
                            }
                        }
                        finally
                        {
                            bmp.UnlockBits(bmpData);
                        }

                        using var ms = new MemoryStream();
                        var encoder = ImageCodecInfo.GetImageDecoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                        if (encoder != null)
                        {
                            using var encoderParams = new EncoderParameters(1);
                            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 75L);
                            bmp.Save(ms, encoder, encoderParams);
                        }
                        else
                        {
                            bmp.Save(ms, ImageFormat.Jpeg);
                        }
                        return ms.ToArray();
                    }
                    finally
                    {
                        // ID3D11DeviceContext::Unmap (vtable index 15)
                        InvokeVTable<UnmapDelegate>(_d3dContextPtr, 15, unmap =>
                            unmap(_d3dContextPtr, stagingPtr, 0));
                    }
                }
                finally
                {
                    if (stagingPtr != IntPtr.Zero) Marshal.Release(stagingPtr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(descPtr);
            }
        }

        // --- vtable invocation helpers ---

        private static void InvokeVTable<TDelegate>(IntPtr comObject, int methodIndex, Action<TDelegate> invoke)
            where TDelegate : Delegate
        {
            var vtable = Marshal.ReadIntPtr(comObject);
            var methodPtr = Marshal.ReadIntPtr(vtable, methodIndex * IntPtr.Size);
            var del = (TDelegate)Marshal.GetDelegateForFunctionPointer(methodPtr, typeof(TDelegate));
            invoke(del);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GetDescDelegate(IntPtr self, IntPtr pDesc);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateTexture2DDelegate(IntPtr self, IntPtr pDesc, IntPtr pInitialData, out IntPtr pp2D);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void CopyResourceDelegate(IntPtr self, IntPtr pDstResource, IntPtr pSrcResource);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int MapDelegate(IntPtr self, IntPtr pResource, uint subresource, uint mapType, uint mapFlags, out D3D11_MAPPED_SUBRESOURCE pMapped);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void UnmapDelegate(IntPtr self, IntPtr pResource, uint subresource);

        [StructLayout(LayoutKind.Sequential)]
        private struct D3D11_TEXTURE2D_DESC
        {
            public uint Width;
            public uint Height;
            public uint MipLevels;
            public uint ArraySize;
            public int Format;
            public uint SampleCount;
            public uint SampleQuality;
            public int Usage;
            public int BindFlags;
            public int CPUAccessFlags;
            public int MiscFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct D3D11_MAPPED_SUBRESOURCE
        {
            public IntPtr pData;
            public uint RowPitch;
            public uint DepthPitch;
        }

        public void Dispose()
        {
            _device = null;
            if (_d3dContextPtr != IntPtr.Zero) { Marshal.Release(_d3dContextPtr); _d3dContextPtr = IntPtr.Zero; }
            if (_d3dDevicePtr != IntPtr.Zero)  { Marshal.Release(_d3dDevicePtr);  _d3dDevicePtr  = IntPtr.Zero; }
        }
    }
}
