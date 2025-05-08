using System.Windows;
using System.Windows.Threading;
using Gma.System.MouseKeyHook;
using System.Windows.Forms; 
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.Drawing.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace AutoMacroWpf
{
    /// <summary>
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private IKeyboardMouseEvents? _globalHook;
        private List<string> _recordedEvents = new List<string>();
        private bool _isRecording = false;
        private DispatcherTimer _playTimer;
        private readonly string recordsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Kayitlar");
        private int _playIndex = 0;
        private bool isPlayingLoaded = false;
        private int repeatCounter = 0;
        private int repeatTarget = 1;
        private bool isScrollTestMode = false;
        private bool isDragging = false;
        private Bitmap stopConditionImage = null;
        private Bitmap continueConditionImage = null;
        private ProgressWindow progressWindow = null;

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        public MainWindow()
        {
            InitializeComponent();
            _playTimer = new DispatcherTimer();
            _playTimer.Tick += PlayTimer_Tick;
            this.KeyDown += MainWindow_KeyDown;
            _globalHook = Hook.GlobalEvents();
            _globalHook.KeyDown += GlobalHook_ShortcutKeyDown;
            _globalHook.MouseWheel += GlobalHook_MouseWheel;
            sliderInterval.ValueChanged += SliderInterval_ValueChanged;
            txtIntervalValue.Text = ((int)sliderInterval.Value).ToString();
        }

        private void btnStartRecording_Click(object? sender, RoutedEventArgs e)
        {
            _recordedEvents.Clear();
            var pos = System.Windows.Forms.Cursor.Position;
            _recordedEvents.Add($"StartPos {pos.X},{pos.Y}");
            _isRecording = true;
            _globalHook.MouseDownExt += GlobalHook_MouseDownExt;
            _globalHook.MouseMove += GlobalHook_MouseMove;
            _globalHook.MouseUpExt += GlobalHook_MouseUpExt;
            _globalHook.KeyDown += GlobalHook_KeyDown;
            _globalHook.MouseWheel += GlobalHook_MouseWheel;
        }

        private void btnStopRecording_Click(object? sender, RoutedEventArgs e)
        {
            _isRecording = false;
            if (_globalHook != null)
            {
                _globalHook.MouseDownExt -= GlobalHook_MouseDownExt;
                _globalHook.MouseMove -= GlobalHook_MouseMove;
                _globalHook.MouseUpExt -= GlobalHook_MouseUpExt;
                _globalHook.KeyDown -= GlobalHook_KeyDown;
                _globalHook.MouseWheel -= GlobalHook_MouseWheel;
            }
            SaveRecordingToFile();
        }

        private void PlayTimer_Tick(object? sender, EventArgs e)
        {
            if (ShouldPauseMacroForImage())
            {
                return;
            }
            if (!isPlayingLoaded) return;
            if (_playIndex == 0 && _recordedEvents.Count > 0 && _recordedEvents[0].StartsWith("StartPos "))
            {
                var parts = _recordedEvents[0].Substring(9).Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                {
                    System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);
                }
                _playIndex++;
                if (_playIndex >= _recordedEvents.Count) return;
            }
            if (_playIndex >= _recordedEvents.Count)
            {
                repeatCounter++;
                if (repeatCounter >= repeatTarget)
                {
                    _playTimer.Stop();
                    isPlayingLoaded = false;
                    _playIndex = 0;
                    if (progressWindow != null)
                    {
                        progressWindow.ShowNotification("Sonlandırıldı", "✅");
                        progressWindow = null;
                    }
                    return;
                }
                _playIndex = 0;
                if (_recordedEvents.Count > 0 && _recordedEvents[0].StartsWith("StartPos "))
                {
                    var parts = _recordedEvents[0].Substring(9).Split(',');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                    {
                        System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);
                    }
                    _playIndex++;
                    if (_playIndex >= _recordedEvents.Count) return;
                }
            }
            SimulateEvent(_recordedEvents[_playIndex]);
            if (progressWindow != null)
            {
                progressWindow.SetProgress(_playIndex + 1, _recordedEvents.Count);
            }
            _playIndex++;
        }

        private void GlobalHook_MouseDownExt(object? sender, MouseEventExtArgs e)
        {
            if (_isRecording && e.Button == MouseButtons.Left)
            {
                isDragging = true;
                _recordedEvents.Add($"MouseDown {e.X},{e.Y}");
            }
            else if (_isRecording)
            {
                string button = "Unknown";
                if (e.Button == MouseButtons.Right)
                    button = "Right";
                else if (e.Button == MouseButtons.Middle)
                    button = "Middle";
                _recordedEvents.Add($"Mouse {button} {e.X},{e.Y}");
            }
        }

        private void GlobalHook_MouseMove(object? sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (_isRecording && isDragging)
            {
                _recordedEvents.Add($"MouseMove {e.X},{e.Y}");
            }
        }

        private void GlobalHook_MouseUpExt(object? sender, MouseEventExtArgs e)
        {
            if (_isRecording && isDragging && e.Button == MouseButtons.Left)
            {
                isDragging = false;
                _recordedEvents.Add($"MouseUp {e.X},{e.Y}");
            }
        }

        private void GlobalHook_KeyDown(object? sender, KeyEventArgs e)
        {
            if (_isRecording && e.KeyCode != Keys.F4)
            {
                List<string> mods = new List<string>();
                if ((Control.ModifierKeys & Keys.Control) == Keys.Control) mods.Add("Ctrl");
                if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt) mods.Add("Alt");
                if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift) mods.Add("Shift");
                if ((Control.ModifierKeys & Keys.LWin) == Keys.LWin || (Control.ModifierKeys & Keys.RWin) == Keys.RWin) mods.Add("Win");
                string modStr = mods.Count > 0 ? string.Join("+", mods) + "+" : "";
                _recordedEvents.Add($"Key {modStr}{e.KeyCode}");
            }
        }

        private void GlobalHook_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (isScrollTestMode)
            {
                txtScrollTestResult.Text = "Mouse tekerleğini bir kez çevirin...";
                txtScrollStep.Text = Math.Abs(e.Delta).ToString();
                isScrollTestMode = false;
                return;
            }
            if (_isRecording)
            {
                string direction = e.Delta > 0 ? "Up" : "Down";
                _recordedEvents.Add($"Scroll {direction} {e.X},{e.Y},{e.Delta}");
            }
        }

        private void SaveRecordingToFile()
        {
            try
            {
                if (!System.IO.Directory.Exists(recordsFolder))
                    System.IO.Directory.CreateDirectory(recordsFolder);
                string fileName = $"kayit_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string filePath = System.IO.Path.Combine(recordsFolder, fileName);
                System.IO.File.WriteAllLines(filePath, _recordedEvents);
            }
            catch (Exception ex)
            {
            }
        }

        private void LoadRecordingFromFile(string filePath)
        {
            try
            {
                var lines = System.IO.File.ReadAllLines(filePath);
                _recordedEvents = new List<string>(lines);
            }
            catch (Exception ex)
            {
            }
        }

        private void btnLoadRecording_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.InitialDirectory = recordsFolder;
            dialog.Filter = "Kayıt Dosyaları (*.txt)|*.txt";
            if (dialog.ShowDialog() == true)
            {
                LoadRecordingFromFile(dialog.FileName);
            }
        }

        private void SimulateEvent(string ev)
        {
            if (ev.StartsWith("MouseDown "))
            {
                var coords = ev.Substring(9).Split(',');
                if (coords.Length == 2 && int.TryParse(coords[0], out int x) && int.TryParse(coords[1], out int y))
                {
                    System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);
                    mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, 0);
                }
            }
            else if (ev.StartsWith("MouseMove "))
            {
                var coords = ev.Substring(9).Split(',');
                if (coords.Length == 2 && int.TryParse(coords[0], out int x) && int.TryParse(coords[1], out int y))
                {
                    System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);
                }
            }
            else if (ev.StartsWith("MouseUp "))
            {
                var coords = ev.Substring(7).Split(',');
                if (coords.Length == 2 && int.TryParse(coords[0], out int x) && int.TryParse(coords[1], out int y))
                {
                    System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);
                    mouse_event(MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
                }
            }
            else if (ev.StartsWith("Mouse "))
            {
                var parts = ev.Split(' ');
                if (parts.Length == 3)
                {
                    var button = parts[1];
                    var coords = parts[2].Split(',');
                    if (coords.Length == 2 && int.TryParse(coords[0], out int x) && int.TryParse(coords[1], out int y))
                    {
                        System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);
                        uint ux = (uint)x;
                        uint uy = (uint)y;
                        if (button == "Left")
                        {
                            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, ux, uy, 0, 0);
                        }
                        else if (button == "Right")
                        {
                            mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, ux, uy, 0, 0);
                        }
                        else if (button == "Middle")
                        {
                            mouse_event(0x0020 | 0x0040, ux, uy, 0, 0);
                        }
                    }
                }
            }
            else if (ev.StartsWith("Scroll "))
            {
                var parts = ev.Split(' ');
                if (parts.Length == 3)
                {
                    var vals = parts[2].Split(',');
                    if (vals.Length == 3 && int.TryParse(vals[0], out int x) && int.TryParse(vals[1], out int y) && int.TryParse(vals[2], out int delta))
                    {
                        System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);
                        int userStep = 120;
                        if (int.TryParse(txtScrollStep.Text, out int val) && val > 0)
                            userStep = val;
                        int playDelta = Math.Sign(delta) * userStep;
                        SimulateScroll(playDelta);
                    }
                }
            }
            else if (ev.StartsWith("Key "))
            {
                var combo = ev.Substring(4);
                SimulateKeyCombo(combo);
            }
        }

        private void SimulateScroll(int delta)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = 0;
            inputs[0].mi.dx = 0;
            inputs[0].mi.dy = 0;
            inputs[0].mi.mouseData = (uint)delta;
            inputs[0].mi.dwFlags = MOUSEEVENTF_WHEEL;
            inputs[0].mi.time = 0;
            inputs[0].mi.dwExtraInfo = IntPtr.Zero;
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
        private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        private const uint MOUSEEVENTF_LEFTUP = 0x04;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const uint MOUSEEVENTF_RIGHTUP = 0x10;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F3)
            {
                btnStartRecording_Click(null, null);
            }
            else if (e.Key == System.Windows.Input.Key.F4)
            {
                btnStopRecording_Click(null, null);
            }
            else if (e.Key == System.Windows.Input.Key.Home)
            {
                btnPlayLoaded_Click(null, null);
            }
            else if (e.Key == System.Windows.Input.Key.End)
            {
                btnStopPlaying_Click(null, null);
            }
        }

        private void GlobalHook_ShortcutKeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F3)
            {
                btnStartRecording_Click(null, null);
            }
            else if (e.KeyCode == Keys.F4)
            {
                btnStopRecording_Click(null, null);
            }
            else if (e.KeyCode == Keys.Home)
            {
                btnPlayLoaded_Click(null, null);
            }
            else if (e.KeyCode == Keys.End)
            {
                btnStopPlaying_Click(null, null);
            }
        }

        private void btnPlayLoaded_Click(object sender, RoutedEventArgs e)
        {
            if (_recordedEvents.Count == 0)
            {
                return;
            }
            isPlayingLoaded = true;
            _playIndex = 0;
            repeatCounter = 0;
            repeatTarget = 1;
            if (rbLoop.IsChecked == true)
            {
                repeatTarget = int.MaxValue;
            }
            else if (rbRepeat.IsChecked == true && int.TryParse(txtRepeatCount.Text, out int val) && val > 0)
            {
                repeatTarget = val;
            }
            _playTimer.Interval = TimeSpan.FromMilliseconds(sliderInterval.Value);
            if (progressWindow == null)
            {
                progressWindow = new ProgressWindow();
                var screen = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                progressWindow.Left = screen.Right - progressWindow.Width - 20;
                progressWindow.Top = screen.Bottom - progressWindow.Height - 40;
                progressWindow.Show();
            }
            progressWindow.SetProgress(0, _recordedEvents.Count);
            _playTimer.Start();
        }

        private void btnStopPlaying_Click(object sender, RoutedEventArgs e)
        {
            isPlayingLoaded = false;
            _playTimer.Stop();
            _playIndex = 0;
            if (progressWindow != null)
            {
                progressWindow.ShowNotification("Sonlandırıldı", "✅");
                progressWindow = null;
            }
        }

        private void btnScrollTest_Click(object sender, RoutedEventArgs e)
        {
            isScrollTestMode = true;
            txtScrollTestResult.Text = "Mouse tekerleğini bir kez çevirin...";
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_globalHook != null)
            {
                _globalHook.KeyDown -= GlobalHook_ShortcutKeyDown;
                _globalHook.Dispose();
            }
            base.OnClosed(e);
        }

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        private const int KEYEVENTF_KEYDOWN = 0x0000;
        private const int KEYEVENTF_KEYUP = 0x0002;

        private void SimulateKeyCombo(string combo)
        {
            var parts = combo.Split('+');
            List<byte> modifiers = new List<byte>();
            byte mainKey = 0;
            foreach (var part in parts)
            {
                switch (part)
                {
                    case "Ctrl": modifiers.Add(0x11); break;
                    case "Alt": modifiers.Add(0x12); break;
                    case "Shift": modifiers.Add(0x10); break;
                    case "Win": modifiers.Add(0x5B); break;
                    default:
                        if (Enum.TryParse(typeof(Keys), part, out var keyObj))
                            mainKey = (byte)(Keys)keyObj;
                        break;
                }
            }
            foreach (var mod in modifiers)
                keybd_event(mod, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            if (mainKey != 0)
                keybd_event(mainKey, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            if (mainKey != 0)
                keybd_event(mainKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            for (int i = modifiers.Count - 1; i >= 0; i--)
                keybd_event(modifiers[i], 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private void SliderInterval_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtIntervalValue != null)
                txtIntervalValue.Text = ((int)sliderInterval.Value).ToString();
        }

        private void sliderInterval_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Home)
            {
                e.Handled = true;
            }
        }

        private void btnLoadStopImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Resim Dosyaları (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp";
            if (dialog.ShowDialog() == true)
            {
                stopConditionImage = new Bitmap(dialog.FileName);
                imgStopPreview.Source = new BitmapImage(new Uri(dialog.FileName));
            }
        }

        private void btnLoadContinueImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Resim Dosyaları (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp";
            if (dialog.ShowDialog() == true)
            {
                continueConditionImage = new Bitmap(dialog.FileName);
                imgContinuePreview.Source = new BitmapImage(new Uri(dialog.FileName));
            }
        }

        private double GetImageSimilarityOpenCV(Bitmap source, Bitmap template)
        {
            using (var sourceMat = BitmapConverter.ToMat(source))
            using (var templateMat = BitmapConverter.ToMat(template))
            using (var result = new Mat())
            {
                Cv2.MatchTemplate(sourceMat, templateMat, result, TemplateMatchModes.CCoeffNormed);
                double minVal, maxVal;
                OpenCvSharp.Point minLoc, maxLoc;
                Cv2.MinMaxLoc(result, out minVal, out maxVal, out minLoc, out maxLoc);
                return maxVal;
            }
        }

        private Bitmap CaptureFullScreen()
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            Bitmap bmp = new Bitmap(screen.Bounds.Width, screen.Bounds.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(screen.Bounds.X, screen.Bounds.Y, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            }
            return bmp;
        }

        private bool ShouldPauseMacroForImage()
        {
            if (stopConditionImage == null && continueConditionImage == null)
                return false;
            Bitmap screenBmp = CaptureFullScreen();
            bool shouldPause = false;
            if (stopConditionImage != null)
            {
                double sim = GetImageSimilarityOpenCV(screenBmp, stopConditionImage);
                if (sim >= 0.9)
                    shouldPause = true;
            }
            if (continueConditionImage != null)
            {
                double sim = GetImageSimilarityOpenCV(screenBmp, continueConditionImage);
                if (sim >= 0.9)
                    shouldPause = false;
            }
            screenBmp.Dispose();
            return shouldPause;
        }
    }
}