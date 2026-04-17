using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SoundRadarOverlay
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new RadarForm());
        }
    }

    internal sealed class RadarForm : Form
    {
        private const int HotkeyEdit = 0x1001;
        private const int HotkeyLock = 0x1002;
        private const int HotkeyExit = 0x1003;
        private const int HotkeyMoveUp = 0x1101;
        private const int HotkeyMoveDown = 0x1102;
        private const int HotkeyMoveLeft = 0x1103;
        private const int HotkeyMoveRight = 0x1104;
        private const int HotkeyGrow = 0x1105;
        private const int HotkeyShrink = 0x1106;
        private const int WmHotkey = 0x0312;
        private const int WmNchittest = 0x0084;
        private const int HtClient = 1;
        private const int HtCaption = 2;
        private const int HtLeft = 10;
        private const int HtRight = 11;
        private const int HtTop = 12;
        private const int HtTopLeft = 13;
        private const int HtTopRight = 14;
        private const int HtBottom = 15;
        private const int HtBottomLeft = 16;
        private const int HtBottomRight = 17;
        private const int GwlExstyle = -20;
        private const int WsExTransparent = 0x20;
        private const int WsExToolwindow = 0x80;
        private const int WsExLayered = 0x80000;
        private const int WsExNoactivate = 0x8000000;
        private const int ModNone = 0x0000;
        private const int ModAlt = 0x0001;
        private const int ModControl = 0x0002;
        private const int ModShift = 0x0004;
        private const Keys EditHotkey = Keys.F8;
        private const Keys LockHotkey = Keys.F9;
        private const Keys ExitHotkey = Keys.F10;
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private const uint SwpNomove = 0x0002;
        private const uint SwpNosize = 0x0001;
        private const uint SwpNoactivate = 0x0010;
        private const uint SwpShowwindow = 0x0040;

        private readonly Timer _renderTimer;
        private readonly Timer _topmostTimer;
        private readonly AudioDirectionMeter _meter;
        private readonly OverlaySettings _settings;
        private readonly string _settingsPath;

        private bool _editMode = true;
        private bool _isAdjustingBounds;
        private float _smoothedAngle;
        private float _smoothedIntensity;
        private float _smoothedSpread;
        private readonly Pen _gridPen = new Pen(Color.FromArgb(90, 255, 255, 255), 1.35f);
        private readonly Pen _midLinePen = new Pen(Color.White, 2f);
        private readonly Pen _baseLinePen = new Pen(Color.FromArgb(120, 80, 165, 255), 3f);
        private readonly Pen _pointerPen = new Pen(Color.FromArgb(225, 255, 70, 70), 3f);
        private readonly Brush _redBrush = new SolidBrush(Color.FromArgb(235, 255, 50, 50));
        private readonly Brush _editBrush = new SolidBrush(Color.FromArgb(120, 0, 120, 255));

        public RadarForm()
        {
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            MinimumSize = new Size(220, 220);

            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SoundRadarOverlay",
                "settings.json");
            _settings = OverlaySettings.Load(_settingsPath);

            Bounds = new Rectangle(_settings.Left, _settings.Top, _settings.Width, _settings.Height);
            _editMode = _settings.EditMode;

            _meter = new AudioDirectionMeter();

            _renderTimer = new Timer { Interval = 33 };
            _renderTimer.Tick += (_, __) => TickOverlay();
            _renderTimer.Start();

            _topmostTimer = new Timer { Interval = 1200 };
            _topmostTimer.Tick += (_, __) => EnsureOverlayState();
            _topmostTimer.Start();

            Load += (_, __) =>
            {
                RegisterHotkeys();
                ApplyInteractionMode();
                ClampToScreen();
                RenderLayered();
            };
            FormClosed += (_, __) =>
            {
                _renderTimer.Stop();
                _topmostTimer.Stop();
                SaveSettings();
                _meter.Dispose();
                UnregisterHotKey(Handle, HotkeyEdit);
                UnregisterHotKey(Handle, HotkeyLock);
                UnregisterHotKey(Handle, HotkeyExit);
                UnregisterHotKey(Handle, HotkeyMoveUp);
                UnregisterHotKey(Handle, HotkeyMoveDown);
                UnregisterHotKey(Handle, HotkeyMoveLeft);
                UnregisterHotKey(Handle, HotkeyMoveRight);
                UnregisterHotKey(Handle, HotkeyGrow);
                UnregisterHotKey(Handle, HotkeyShrink);
            };
            LocationChanged += (_, __) =>
            {
                ClampToScreen();
                SaveSettings();
                RenderLayered();
            };
            SizeChanged += (_, __) =>
            {
                ClampToScreen();
                SaveSettings();
                RenderLayered();
            };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, __) =>
            {
                ClampToScreen();
                RenderLayered();
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WsExLayered | WsExToolwindow | WsExNoactivate;
                if (!_editMode)
                {
                    cp.ExStyle |= WsExTransparent;
                }

                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey)
            {
                switch (m.WParam.ToInt32())
                {
                    case HotkeyEdit:
                        _editMode = true;
                        ApplyInteractionMode();
                        break;
                    case HotkeyLock:
                        _editMode = !_editMode;
                        ApplyInteractionMode();
                        break;
                    case HotkeyExit:
                        Close();
                        break;
                    case HotkeyMoveUp:
                        AdjustOverlay(0, -10, 0);
                        break;
                    case HotkeyMoveDown:
                        AdjustOverlay(0, 10, 0);
                        break;
                    case HotkeyMoveLeft:
                        AdjustOverlay(-10, 0, 0);
                        break;
                    case HotkeyMoveRight:
                        AdjustOverlay(10, 0, 0);
                        break;
                    case HotkeyGrow:
                        AdjustOverlay(0, 0, 20);
                        break;
                    case HotkeyShrink:
                        AdjustOverlay(0, 0, -20);
                        break;
                }
            }

            if (m.Msg == WmNchittest && _editMode)
            {
                base.WndProc(ref m);
                if ((int)m.Result == HtClient)
                {
                    m.Result = (IntPtr)GetResizeHitTest();
                    if ((int)m.Result == HtClient)
                    {
                        m.Result = (IntPtr)HtCaption;
                    }
                }
                return;
            }

            base.WndProc(ref m);
        }

        private void TickOverlay()
        {
            var reading = _meter.GetReading();
            var targetAngle = reading.Balance * 80f;
            var targetIntensity = reading.Level;
            var targetSpread = reading.Spread;

            _smoothedAngle = Lerp(_smoothedAngle, targetAngle, 0.22f);
            _smoothedIntensity = Lerp(_smoothedIntensity, targetIntensity, 0.16f);
            _smoothedSpread = Lerp(_smoothedSpread, targetSpread, 0.18f);
            RenderLayered();
        }

        private void RenderRadar(Graphics g, Rectangle rect)
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var diameter = Math.Min(rect.Width, rect.Height) - 24;
            var radarRect = new Rectangle(
                rect.X + (rect.Width - diameter) / 2,
                rect.Y + (rect.Height - diameter) / 2,
                diameter,
                diameter);
            var center = new PointF(radarRect.Left + radarRect.Width / 2f, radarRect.Top + radarRect.Height / 2f);
            var radius = radarRect.Width / 2f;

            g.DrawEllipse(_gridPen, radarRect);
            g.DrawEllipse(_gridPen, Inflate(radarRect, -(int)(radius * 0.30f)));
            g.DrawEllipse(_gridPen, Inflate(radarRect, -(int)(radius * 0.58f)));

            g.DrawLine(_midLinePen, center.X, radarRect.Top + 8, center.X, radarRect.Bottom - 8);
            g.DrawLine(_baseLinePen, radarRect.Left + 10, center.Y, radarRect.Right - 10, center.Y);

            DrawSweep(g, center, radius * 0.82f);
            if (_editMode)
            {
                var grip = new Rectangle(rect.Right - 24, rect.Bottom - 24, 16, 16);
                g.FillRectangle(_editBrush, grip);
                g.DrawRectangle(Pens.White, grip);
            }
        }

        private void RenderLayered()
        {
            if (!IsHandleCreated || Width <= 0 || Height <= 0)
            {
                return;
            }

            using (var bitmap = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                RenderRadar(graphics, new Rectangle(0, 0, Width, Height));
                ApplyBitmap(bitmap);
            }
        }

        private void ApplyBitmap(Bitmap bitmap)
        {
            var screenDc = GetDC(IntPtr.Zero);
            var memoryDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memoryDc, hBitmap);

                var size = new SizeStruct(bitmap.Width, bitmap.Height);
                var sourcePoint = new PointStruct(0, 0);
                var topPos = new PointStruct(Left, Top);
                var blend = new BlendFunction
                {
                    BlendOp = AcSrcOver,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AcSrcAlpha
                };

                UpdateLayeredWindow(Handle, screenDc, ref topPos, ref size, memoryDc, ref sourcePoint, 0, ref blend, UlwAlpha);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero)
                {
                    SelectObject(memoryDc, oldBitmap);
                }

                if (hBitmap != IntPtr.Zero)
                {
                    DeleteObject(hBitmap);
                }

                DeleteDC(memoryDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private void DrawSweep(Graphics g, PointF center, float maxRadius)
        {
            var angleRadians = (float)((Math.PI / 180.0) * (_smoothedAngle - 90f));
            var pointerRadius = maxRadius * (0.32f + _smoothedIntensity * 0.68f);
            var end = new PointF(
                center.X + (float)Math.Cos(angleRadians) * pointerRadius,
                center.Y + (float)Math.Sin(angleRadians) * pointerRadius);

            _pointerPen.Width = 2.5f + (_smoothedIntensity * 2f);
            g.DrawLine(_pointerPen, center, end);

            var indicatorSize = 12f + (_smoothedIntensity * 8f);
            g.FillEllipse(_redBrush, end.X - indicatorSize / 2f, end.Y - indicatorSize / 2f, indicatorSize, indicatorSize);

            if (_smoothedSpread > 0.08f)
            {
                var spreadDegrees = 8f + (_smoothedSpread * 24f);
                using (var spreadPen = new Pen(Color.FromArgb(90, 255, 180, 70), 1.8f))
                {
                    g.DrawArc(
                        spreadPen,
                        center.X - pointerRadius,
                        center.Y - pointerRadius,
                        pointerRadius * 2,
                        pointerRadius * 2,
                        _smoothedAngle - spreadDegrees - 90f,
                        spreadDegrees * 2f);
                }
            }
        }

        private int GetResizeHitTest()
        {
            var cursor = PointToClient(Cursor.Position);
            const int grip = 12;
            var left = cursor.X <= grip;
            var right = cursor.X >= Width - grip;
            var top = cursor.Y <= grip;
            var bottom = cursor.Y >= Height - grip;

            if (left && top) return HtTopLeft;
            if (right && top) return HtTopRight;
            if (left && bottom) return HtBottomLeft;
            if (right && bottom) return HtBottomRight;
            if (left) return HtLeft;
            if (right) return HtRight;
            if (top) return HtTop;
            if (bottom) return HtBottom;
            return HtClient;
        }

        private void ApplyInteractionMode()
        {
            SaveSettings();
            var exStyle = GetWindowLong(Handle, GwlExstyle);
            exStyle |= WsExLayered | WsExToolwindow | WsExNoactivate;
            if (_editMode)
            {
                exStyle &= ~WsExTransparent;
            }
            else
            {
                exStyle |= WsExTransparent;
            }

            SetWindowLong(Handle, GwlExstyle, exStyle);
            EnsureOverlayState();
            RenderLayered();
        }

        private void RegisterHotkeys()
        {
            RegisterHotKey(Handle, HotkeyEdit, ModNone, (int)EditHotkey);
            RegisterHotKey(Handle, HotkeyLock, ModNone, (int)LockHotkey);
            RegisterHotKey(Handle, HotkeyExit, ModNone, (int)ExitHotkey);
            RegisterHotKey(Handle, HotkeyMoveUp, ModControl | ModAlt, (int)Keys.Up);
            RegisterHotKey(Handle, HotkeyMoveDown, ModControl | ModAlt, (int)Keys.Down);
            RegisterHotKey(Handle, HotkeyMoveLeft, ModControl | ModAlt, (int)Keys.Left);
            RegisterHotKey(Handle, HotkeyMoveRight, ModControl | ModAlt, (int)Keys.Right);
            RegisterHotKey(Handle, HotkeyGrow, ModControl | ModAlt | ModShift, (int)Keys.Up);
            RegisterHotKey(Handle, HotkeyShrink, ModControl | ModAlt | ModShift, (int)Keys.Down);
        }

        private void SaveSettings()
        {
            _settings.Left = Left;
            _settings.Top = Top;
            _settings.Width = Width;
            _settings.Height = Height;
            _settings.EditMode = _editMode;
            _settings.Save(_settingsPath);
        }

        private void EnsureOverlayState()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            TopMost = true;
            SetWindowPos(
                Handle,
                HwndTopmost,
                0,
                0,
                0,
                0,
                SwpNomove | SwpNosize | SwpNoactivate | SwpShowwindow);
        }

        private void AdjustOverlay(int deltaX, int deltaY, int sizeDelta)
        {
            if (!IsHandleCreated)
            {
                return;
            }

            var newWidth = Math.Max(MinimumSize.Width, Width + sizeDelta);
            var newHeight = Math.Max(MinimumSize.Height, Height + sizeDelta);
            var newLeft = Left + deltaX;
            var newTop = Top + deltaY;

            if (sizeDelta != 0)
            {
                newLeft -= sizeDelta / 2;
                newTop -= sizeDelta / 2;
            }

            Bounds = new Rectangle(newLeft, newTop, newWidth, newHeight);
            ClampToScreen();
            SaveSettings();
            RenderLayered();
        }

        private void ClampToScreen()
        {
            if (_isAdjustingBounds || !IsHandleCreated)
            {
                return;
            }

            _isAdjustingBounds = true;
            try
            {
                var screen = Screen.FromRectangle(Bounds);
                var area = screen.Bounds;
                var left = Left;
                var top = Top;

                if (Width > area.Width)
                {
                    Width = area.Width;
                }

                if (Height > area.Height)
                {
                    Height = area.Height;
                }

                left = Math.Max(area.Left, Math.Min(left, area.Right - Width));
                top = Math.Max(area.Top, Math.Min(top, area.Bottom - Height));

                const int snapDistance = 20;
                if (Math.Abs(left - area.Left) <= snapDistance)
                {
                    left = area.Left;
                }
                else if (Math.Abs((left + Width) - area.Right) <= snapDistance)
                {
                    left = area.Right - Width;
                }

                if (Math.Abs(top - area.Top) <= snapDistance)
                {
                    top = area.Top;
                }
                else if (Math.Abs((top + Height) - area.Bottom) <= snapDistance)
                {
                    top = area.Bottom - Height;
                }

                if (left != Left || top != Top)
                {
                    Location = new Point(left, top);
                }
            }
            finally
            {
                _isAdjustingBounds = false;
            }
        }

        private static Rectangle Inflate(Rectangle rect, int amount)
        {
            return new Rectangle(rect.X - amount, rect.Y - amount, rect.Width + amount * 2, rect.Height + amount * 2);
        }

        private static float Lerp(float current, float target, float amount)
        {
            return current + ((target - current) * amount);
        }

        private const byte AcSrcOver = 0x00;
        private const byte AcSrcAlpha = 0x01;
        private const int UlwAlpha = 0x00000002;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr hwnd,
            IntPtr hdcDst,
            ref PointStruct pptDst,
            ref SizeStruct psize,
            IntPtr hdcSrc,
            ref PointStruct pprSrc,
            int crKey,
            ref BlendFunction pblend,
            int dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hDc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hDc, IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct PointStruct
        {
            public int X;
            public int Y;

            public PointStruct(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SizeStruct
        {
            public int CX;
            public int CY;

            public SizeStruct(int cx, int cy)
            {
                CX = cx;
                CY = cy;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }
    }

    internal sealed class AudioDirectionMeter : IDisposable
    {
        private IMMDeviceEnumerator _deviceEnumerator;
        private IMMDevice _device;
        private IAudioMeterInformation _meter;

        public AudioDirectionMeter()
        {
            _deviceEnumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            Initialize();
        }

        public AudioReading GetReading()
        {
            try
            {
                if (_meter == null)
                {
                    Initialize();
                    return new AudioReading();
                }

                int count;
                _meter.GetMeteringChannelCount(out count);
                if (count <= 0)
                {
                    return new AudioReading();
                }

                var peaks = new float[count];
                _meter.GetChannelsPeakValues(count, peaks);

                var left = peaks[0];
                var right = count > 1 ? peaks[1] : peaks[0];
                var overall = Math.Max(left, right);
                var balance = (left + right) < 0.0001f ? 0f : (right - left) / Math.Max(0.05f, left + right);
                var spread = Math.Abs(left - right);

                return new AudioReading
                {
                    Level = Clamp(overall, 0f, 1f),
                    Balance = Clamp(balance, -1f, 1f),
                    Spread = Clamp(spread, 0f, 1f)
                };
            }
            catch
            {
                Initialize();
                return new AudioReading();
            }
        }

        private void Initialize()
        {
            ReleaseCom(ref _meter);
            ReleaseCom(ref _device);

            _deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out _device);
            var iid = typeof(IAudioMeterInformation).GUID;
            object meterObject;
            _device.Activate(ref iid, 23, IntPtr.Zero, out meterObject);
            _meter = (IAudioMeterInformation)meterObject;
        }

        public void Dispose()
        {
            ReleaseCom(ref _meter);
            ReleaseCom(ref _device);
            ReleaseCom(ref _deviceEnumerator);
        }

        private static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static void ReleaseCom<T>(ref T comObject) where T : class
        {
            if (comObject != null)
            {
                try
                {
                    Marshal.ReleaseComObject(comObject);
                }
                catch
                {
                }
            }

            comObject = null;
        }
    }

    internal struct AudioReading
    {
        public float Level;
        public float Balance;
        public float Spread;
    }

    internal sealed class OverlaySettings
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool EditMode { get; set; }

        public OverlaySettings()
        {
            Left = 50;
            Top = 50;
            Width = 360;
            Height = 360;
            EditMode = true;
        }

        public static OverlaySettings Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);
                    if (lines.Length >= 5)
                    {
                        return new OverlaySettings
                        {
                            Left = ParseInt(lines[0], 50),
                            Top = ParseInt(lines[1], 50),
                            Width = ParseInt(lines[2], 360),
                            Height = ParseInt(lines[3], 360),
                            EditMode = ParseBool(lines[4], true)
                        };
                    }
                }
            }
            catch
            {
            }

            return new OverlaySettings();
        }

        public void Save(string path)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllLines(path, new[]
                {
                    Left.ToString(),
                    Top.ToString(),
                    Width.ToString(),
                    Height.ToString(),
                    EditMode.ToString()
                });
            }
            catch
            {
            }
        }

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : fallback;
        }
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumerator
    {
    }

    internal enum EDataFlow
    {
        eRender,
        eCapture,
        eAll
    }

    internal enum ERole
    {
        eConsole,
        eMultimedia,
        eCommunications
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, int dwStateMask, out object ppDevices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
        int RegisterEndpointNotificationCallback(IntPtr pClient);
        int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        int GetState(out int pdwState);
    }

    [ComImport]
    [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioMeterInformation
    {
        int GetPeakValue(out float pfPeak);
        int GetMeteringChannelCount(out int pnChannelCount);
        int GetChannelsPeakValues(int u32ChannelCount, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] float[] afPeakValues);
        int QueryHardwareSupport(out int pdwHardwareSupportMask);
    }
}
