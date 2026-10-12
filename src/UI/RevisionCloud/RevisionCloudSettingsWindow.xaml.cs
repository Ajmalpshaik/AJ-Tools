using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using AJTools.Models.RevisionCloud;
using AJTools.Utils;

namespace AJTools.UI.RevisionCloud
{
    public partial class RevisionCloudSettingsWindow : Window
    {
        public RevisionCloudSettings Settings { get; private set; }
        public bool Confirmed { get; private set; }

        public RevisionCloudSettingsWindow(RevisionCloudSettings settings)
        {
            InitializeComponent();

            // Shared AJ Tools window entrance (fade + short rise). Cosmetic only.
            WindowMotionHelper.AttachStandardEntrance(this);

            // Shared AJ Tools window exit (fade + short sink). The window's result,
            // validation and close behaviour are unchanged - see WindowMotionHelper's header.
            WindowMotionHelper.AttachStandardExit(this);
            Settings = settings;

            txtOffset.Text = Settings.OffsetDistanceMm.ToString(CultureInfo.InvariantCulture);
            Validate(out _);
        }

        private void TxtOffset_TextChanged(object sender, TextChangedEventArgs e)
        {
            Validate(out _);
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            if (!Validate(out double offset))
                return;

            Settings.OffsetDistanceMm = offset;

            if (!Settings.Save(out string saveError))
            {
                // Stay open: closing here would look exactly like a successful save.
                ShowError("The offset could not be saved: " + saveError);
                return;
            }

            Confirmed = true;
            Close();
        }

        /// <summary>
        /// Checks the offset box, shows any problem inline (the window stays open - no popup) and
        /// only enables Save for a usable value.
        /// </summary>
        private bool Validate(out double offset)
        {
            bool valid = TryParseOffset(txtOffset?.Text, out offset, out string error);
            ShowError(valid ? string.Empty : error);

            if (SaveButton != null)
                SaveButton.IsEnabled = valid;

            return valid;
        }

        private void ShowError(string text)
        {
            if (ErrorText != null)
                ErrorText.Text = text ?? string.Empty;
        }

        /// <summary>
        /// Reads a millimetre value typed with this PC's decimal separator or with a point. Thousands
        /// separators are not accepted: the old parse allowed them, so "12,5" silently became 125. A
        /// value that would read as two different numbers is refused instead of guessed.
        /// </summary>
        private static bool TryParseOffset(string text, out double offset, out string error)
        {
            const string NumberHint = "Enter a number of 0 or more, like 50 or 12.5.";

            offset = 0;
            error = null;

            string trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                error = NumberHint;
                return false;
            }

            bool currentOk = double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out double currentValue);
            bool invariantOk = double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double invariantValue);

            if (currentOk && invariantOk && !currentValue.Equals(invariantValue))
            {
                error = "That number can be read two ways - type it like 12.5.";
                return false;
            }

            if (!currentOk && !invariantOk)
            {
                error = NumberHint;
                return false;
            }

            offset = currentOk ? currentValue : invariantValue;
            if (double.IsNaN(offset) || double.IsInfinity(offset) || offset < 0)
            {
                offset = 0;
                error = NumberHint;
                return false;
            }

            return true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            Close();
        }
    }
}
