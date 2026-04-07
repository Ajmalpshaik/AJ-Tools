// Tool Name: About
// Description: Displays information about the AJ Tools add-in in a dedicated About window.
// Author: Ajmal P.S.
// Version: 1.2.0
// Last Updated: 2026-04-07
// Revit Version: 2020
// Dependencies: Autodesk.Revit.UI, System.Windows.Forms, System.Drawing
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace AJTools.Commands
{
    /// <summary>
    /// Shows About information for AJ Tools.
    /// </summary>
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.ReadOnly)]
    public class CmdAbout : IExternalCommand
    {
        private const string LinkedInUrl = "https://www.linkedin.com/in/ajmalps/";
        private const string EmailAddress = "ajmalnattika@gmail.com";
        private const string OptionalPhotoFileName = "AboutPhoto.png";
        private const int RecommendedPhotoWidthPx = 300;
        private const int RecommendedPhotoHeightPx = 300;
        private const int RecommendedPhotoMaxKb = 500;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                ShowAboutDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("AJ Tools - About", "Unable to open About window.\n\n" + ex.Message);
                return Result.Failed;
            }
        }

        private static void ShowAboutDialog()
        {
            string version = GetVersionString();
            string photoPath = ResolvePhotoPath();

            using (var form = new WinForms.Form())
            using (var title = new WinForms.Label())
            using (var versionLabel = new WinForms.Label())
            using (var picture = new WinForms.PictureBox())
            using (var highlights = new WinForms.TextBox())
            using (var contact = new WinForms.Label())
            using (var linkedin = new WinForms.LinkLabel())
            using (var email = new WinForms.LinkLabel())
            using (var photoHelp = new WinForms.Label())
            using (var close = new WinForms.Button())
            {
                form.Text = "About AJ Tools";
                form.FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
                form.StartPosition = WinForms.FormStartPosition.CenterScreen;
                form.ClientSize = new Drawing.Size(620, 430);
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                title.AutoSize = true;
                title.Location = new Drawing.Point(16, 14);
                title.Font = new Drawing.Font(form.Font.FontFamily, 13, Drawing.FontStyle.Bold);
                title.Text = "AJ Tools for Revit 2020";

                versionLabel.AutoSize = true;
                versionLabel.Location = new Drawing.Point(18, 42);
                versionLabel.ForeColor = Drawing.Color.DimGray;
                versionLabel.Text = "Version: " + version;

                picture.Location = new Drawing.Point(18, 72);
                picture.Size = new Drawing.Size(160, 160);
                picture.BorderStyle = WinForms.BorderStyle.FixedSingle;
                picture.SizeMode = WinForms.PictureBoxSizeMode.Zoom;

                Drawing.Bitmap photo = TryLoadBitmap(photoPath);
                if (photo != null)
                {
                    picture.Image = photo;
                }
                else
                {
                    picture.BackColor = Drawing.Color.WhiteSmoke;
                }

                highlights.Location = new Drawing.Point(195, 72);
                highlights.Size = new Drawing.Size(407, 220);
                highlights.Multiline = true;
                highlights.ReadOnly = true;
                highlights.BorderStyle = WinForms.BorderStyle.None;
                highlights.BackColor = form.BackColor;
                highlights.TabStop = false;
                highlights.Text =
                    "Lightweight productivity tools for daily documentation.\r\n\r\n" +
                    "Highlights:\r\n" +
                    "- Smart MEP Tag: intelligent placement with category-wise offset settings\r\n" +
                    "- Arrange Tags: vertical stack arrangement with L1/T1 direction logic\r\n" +
                    "- L-Shape Leader: improved elbow behavior with outside-text correction\r\n" +
                    "- Graphics: toggle links, unhide all, reset overrides\r\n" +
                    "- Dimensions: auto/grid/level tools, dim by line\r\n" +
                    "- Datums: reset grids/levels back to 3D extents";

                contact.AutoSize = true;
                contact.Location = new Drawing.Point(18, 252);
                contact.Text = "Developer: Ajmal P.S.";

                linkedin.AutoSize = true;
                linkedin.Location = new Drawing.Point(18, 276);
                linkedin.Text = "LinkedIn Profile";
                linkedin.LinkClicked += (s, e) => OpenExternal(LinkedInUrl);

                email.AutoSize = true;
                email.Location = new Drawing.Point(140, 276);
                email.Text = EmailAddress;
                email.LinkClicked += (s, e) => OpenExternal("mailto:" + EmailAddress);

                photoHelp.AutoSize = true;
                photoHelp.Location = new Drawing.Point(18, 315);
                photoHelp.ForeColor = Drawing.Color.DimGray;
                photoHelp.Text =
                    "Optional photo: place Resources\\" + OptionalPhotoFileName +
                    $" ({RecommendedPhotoWidthPx}x{RecommendedPhotoHeightPx} px PNG, <= {RecommendedPhotoMaxKb} KB).";

                close.Text = "Close";
                close.DialogResult = WinForms.DialogResult.OK;
                close.Size = new Drawing.Size(80, 30);
                close.Location = new Drawing.Point(522, 382);

                form.Controls.Add(title);
                form.Controls.Add(versionLabel);
                form.Controls.Add(picture);
                form.Controls.Add(highlights);
                form.Controls.Add(contact);
                form.Controls.Add(linkedin);
                form.Controls.Add(email);
                form.Controls.Add(photoHelp);
                form.Controls.Add(close);
                form.AcceptButton = close;
                form.ShowDialog();

                if (picture.Image != null)
                    picture.Image.Dispose();
            }
        }

        private static string GetVersionString()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null)
                return "Unknown";
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        private static string ResolvePhotoPath()
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            return Path.Combine(assemblyDir, "Resources", OptionalPhotoFileName);
        }

        private static Drawing.Bitmap TryLoadBitmap(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Drawing.Image.FromStream(fs))
                {
                    return new Drawing.Bitmap(img);
                }
            }
            catch
            {
                return null;
            }
        }

        private static void OpenExternal(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch
            {
                TaskDialog.Show("AJ Tools - About", "Unable to open:\n" + target);
            }
        }
    }
}
