using System.Windows;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AutoMacroWpf
{
    public partial class ProgressWindow : Window
    {
        public double ProgressAngle { get; set; } = 0;

        public ProgressWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public void SetProgress(int current, int total)
        {
            progressPanel.Visibility = Visibility.Visible;
            notificationPanel.Visibility = Visibility.Collapsed;
            if (total <= 0) { txtPercent.Text = "0%"; ProgressAngle = 0; return; }
            double percent = (current * 100.0) / total;
            txtPercent.Text = $"{(int)percent}%";
            ProgressAngle = percent * 3.6; 
            txtStatus.Text = "Makro çalışıyor";
            DataContext = null;
            DataContext = this;
        }

        public async void ShowNotification(string message, string emoji = "🔔", int durationMs = 1800)
        {
            progressPanel.Visibility = Visibility.Collapsed;
            notificationPanel.Visibility = Visibility.Visible;
            txtEmoji.Text = emoji;
            txtNotification.Text = message;
            await Task.Delay(durationMs);
            this.Close();
        }
    }
} 