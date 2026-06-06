#nullable disable
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.D3DCompiler;
using Vortice.Mathematics;
using System.IO;

namespace EasyShader
{
    public class D3D11Renderer : HwndHost
    {
        private ID3D11Device _device; private ID3D11DeviceContext _context;
        private IDXGISwapChain _swapChain; private ID3D11RenderTargetView _renderTargetView;
        private ID3D11Buffer _constantBuffer; private ID3D11PixelShader _pixelShader;
        private ID3D11VertexShader _vertexShader; private Stopwatch _timer = new Stopwatch();
        private IntPtr _hwnd; private bool _isReady = false;
        private int _lastW = 0; private int _lastH = 0;

        private ID3D11Texture2D _backBufferTexture;

        [StructLayout(LayoutKind.Sequential)]
        private struct ShaderConstants { public float iTime; public float iResolutionX; public float iResolutionY; public float padding; }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            _hwnd = CreateWindowEx(0, "static", "", 0x40000000 | 0x10000000, 0, 0, 100, 100, hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => InitializeDirectX()), DispatcherPriority.Loaded);
            return new HandleRef(this, _hwnd);
        }
        protected override void DestroyWindowCore(HandleRef hwnd) { DestroyWindow(hwnd.Handle); }
        [DllImport("user32.dll")] internal static extern IntPtr CreateWindowEx(int dwExStyle, string lpszClassName, string lpszWindowName, int style, int x, int y, int width, int height, IntPtr hwndParent, IntPtr hMenu, IntPtr hInst, IntPtr pvParam);
        [DllImport("user32.dll")] internal static extern bool DestroyWindow(IntPtr hwnd);

        private void InitializeDirectX()
        {
            try
            {
                var desc = new SwapChainDescription()
                {
                    BufferCount = 1,
                    BufferDescription = new ModeDescription(800, 600, Format.B8G8R8A8_UNorm),
                    Windowed = true,
                    OutputWindow = _hwnd,
                    SampleDescription = new SampleDescription(1, 0),
                    SwapEffect = SwapEffect.Discard,
                    BufferUsage = Usage.RenderTargetOutput
                };

                D3D11.D3D11CreateDeviceAndSwapChain(null, DriverType.Hardware, DeviceCreationFlags.None, new[] { FeatureLevel.Level_11_0, FeatureLevel.Level_10_0 }, desc, out _swapChain, out _device, out _, out _context);
                RecreateRenderTarget(800, 600);
                _constantBuffer = _device.CreateBuffer(new BufferDescription(16, BindFlags.ConstantBuffer, ResourceUsage.Default));
                string vs = "struct VSOut { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; }; VSOut main(uint id : SV_VertexID) { VSOut output; output.uv = float2((id << 1) & 2, id & 2); output.pos = float4(output.uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0); output.uv.y = 1.0 - output.uv.y; return output; }";
                Compiler.Compile(vs, "main", "main", "vs_4_0", out var vsBlob, out _);
                _vertexShader = _device.CreateVertexShader(vsBlob.AsBytes());
                CompositionTarget.Rendering += RenderFrame; _timer.Start(); _isReady = true;
            }
            catch { }
        }

        private void RecreateRenderTarget(int w, int h)
        {
            if (_renderTargetView != null) _renderTargetView.Dispose();
            if (_backBufferTexture != null) _backBufferTexture.Dispose();
            _swapChain.ResizeBuffers(0, (uint)w, (uint)h, Format.Unknown, SwapChainFlags.None);
            _backBufferTexture = _swapChain.GetBuffer<ID3D11Texture2D>(0);
            _renderTargetView = _device.CreateRenderTargetView(_backBufferTexture);
            _lastW = w; _lastH = h;
        }

        public string CompileAndSetShader(string hlsl)
        {
            if (!_isReady) return "Wait...";
            if (string.IsNullOrWhiteSpace(hlsl))
            {
                if (_pixelShader != null) { _pixelShader.Dispose(); _pixelShader = null; }
                return "Success";
            }
            try
            {
                var res = Compiler.Compile(hlsl, "main", "main", "ps_4_0", out var psBlob, out var err);
                if (psBlob == null) return err?.AsString() ?? "Error";
                var shader = _device.CreatePixelShader(psBlob.AsBytes());
                if (_pixelShader != null) _pixelShader.Dispose();
                _pixelShader = shader; return "Success";
            }
            catch (Exception ex) { return ex.Message; }
        }

        private void RenderFrame(object s, EventArgs e)
        {
            if (!_isReady) return;
            GetClientRect(_hwnd, out var rect);
            int w = rect.Right - rect.Left; int h = rect.Bottom - rect.Top;
            if (w <= 0 || h <= 0) return;
            if (w != _lastW || h != _lastH) RecreateRenderTarget(w, h);
            _context.OMSetRenderTargets(_renderTargetView);
            _context.ClearRenderTargetView(_renderTargetView, new Color4(0, 0, 0, 1));
            if (_pixelShader != null)
            {
                var data = new ShaderConstants { iTime = (float)_timer.Elapsed.TotalSeconds, iResolutionX = (float)w, iResolutionY = (float)h };
                _context.UpdateSubresource(data, _constantBuffer);
                _context.RSSetViewport(new Viewport(0, 0, w, h));
                _context.VSSetShader(_vertexShader); _context.PSSetShader(_pixelShader);
                _context.PSSetConstantBuffer(0, _constantBuffer); _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
                _context.Draw(3, 0);
            }
            _swapChain.Present(1, PresentFlags.None);
        }

        public bool SaveScreenshot(string filePath)
        {
            if (!_isReady || _backBufferTexture == null) return false;
            try
            {
                var desc = _backBufferTexture.Description;
                desc.Usage = ResourceUsage.Staging;
                desc.BindFlags = BindFlags.None;
                desc.CPUAccessFlags = CpuAccessFlags.Read;
                desc.MiscFlags = ResourceOptionFlags.None;

                using (var stagingTexture = _device.CreateTexture2D(desc))
                {
                    _context.CopyResource(stagingTexture, _backBufferTexture);
                    var mapped = _context.Map(stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                    try
                    {
                        // ✅ 现在 DX 和 GDI+ 都是 BGRA 格式，直接保存，颜色 100% 还原！
                        using (var bmp = new System.Drawing.Bitmap((int)desc.Width, (int)desc.Height, (int)mapped.RowPitch, System.Drawing.Imaging.PixelFormat.Format32bppArgb, mapped.DataPointer))
                        {
                            bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                        }
                    }
                    finally
                    {
                        _context.Unmap(stagingTexture, 0);
                    }
                }
                return true;
            }
            catch { return false; }
        }

        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    }
}