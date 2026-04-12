using System.Globalization;
using System.Windows;
using AJTools.Models.RevisionCloud;

namespace AJTools.UI.RevisionCloud
{
    public partial class RevisionCloudSettingsWindow : Window
    {
        public RevisionCloudSettings Settings { get; private set; }
        public bool Confirmed { get; private set; }

        public RevisionCloudSettingsWindow(RevisionCloudSettings settings)
        {
            InitializeComponent();
            Settings = settings;

            txtOffset.Text = Settings.OffsetDistanceMm.ToString(CultureInfo.InvariantCulture);
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(txtOffset.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double offset) || offset < 0)
            {
                MessageBox.Show("Offset distance must be a number >= 0.", "Invalid Input");
                return;
            }
            Settings.OffsetDistanceMm = offset;

            Confirmed = true;
            Settings.Save();
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }
    }
}
