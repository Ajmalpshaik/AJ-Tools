// Tool Name: About
// Description: Displays information about the AJ Tools add-in in a dedicated About window.
// Author: Ajmal P.S.
// Version: 1.2.1
// Last Updated: 2026-04-09
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

        private static readonly Drawing.Color HeaderBackColor = Drawing.Color.FromArgb(23, 64, 105);
        private static readonly Drawing.Color HeaderSubTextColor = Drawing.Color.FromArgb(225, 234, 245);
        private static readonly Drawing.Color AccentColor = Drawing.Color.FromArgb(25, 98, 166);
        private static readonly Drawing.Color AccentHoverColor = Drawing.Color.FromArgb(16, 77, 132);
        private static readonly Drawing.Color CardBackColor = Drawing.Color.FromArgb(246, 249, 253);
        private static readonly Drawing.Color BorderColor = Drawing.Color.FromArgb(209, 220, 232);
        private static readonly Drawing.Color BodyTextColor = Drawing.Color.FromArgb(34, 42, 53);
        private static readonly Drawing.Color MutedTextColor = Drawing.Color.FromArgb(91, 103, 118);

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
            Drawing.Bitmap photo = TryLoadBitmap(ResolvePhotoPath()) ?? CreateProfilePlaceholderBitmap();

            using (var form = new WinForms.Form())
            {
                form.Text = "About AJ Tools";
                form.FormBorderStyle = WinForms.FormBorderStyle.FixedDialog;
                form.StartPosition = WinForms.FormStartPosition.CenterScreen;
                form.ClientSize = new Drawing.Size(820, 540);
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.BackColor = Drawing.Color.White;
                form.Font = new Drawing.Font("Segoe UI", 9F, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Point);

                WinForms.Button closeButton;
                var header = CreateHeaderPanel(version);
                var body = CreateBodyPanel(photo);
                var footer = CreateFooterPanel(out closeButton);

                form.Controls.Add(body);
                form.Controls.Add(footer);
                form.Controls.Add(header);
                form.AcceptButton = closeButton;

                form.FormClosed += (sender, args) =>
                {
                    photo.Dispose();
                };

                form.ShowDialog();
            }
        }

        private static WinForms.Control CreateHeaderPanel(string version)
        {
            var headerPanel = new WinForms.Panel
            {
                Dock = WinForms.DockStyle.Top,
                Height = 104,
                BackColor = HeaderBackColor,
                Padding = new WinForms.Padding(18, 14, 18, 14)
            };

            var headerLayout = new WinForms.TableLayoutPanel
            {
                Dock = WinForms.DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = HeaderBackColor
            };
            headerLayout.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Percent, 100F));
            headerLayout.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.AutoSize));

            var titleStack = new WinForms.TableLayoutPanel
            {
                Dock = WinForms.DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = HeaderBackColor
            };
            titleStack.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.Percent, 100F));
            titleStack.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.AutoSize));

            var title = new WinForms.Label
            {
                AutoSize = true,
                ForeColor = Drawing.Color.White,
                Font = new Drawing.Font("Segoe UI Semibold", 20F, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Point),
                Text = "AJ Tools",
                Margin = new WinForms.Padding(0, 0, 0, 2)
            };

            var subtitle = new WinForms.Label
            {
                AutoSize = true,
                ForeColor = HeaderSubTextColor,
                Font = new Drawing.Font("Segoe UI", 10F, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Point),
                Text = "Revit 2020 productivity toolkit for documentation and coordination"
            };

            var versionBadge = new WinForms.Label
            {
                AutoSize = true,
                Anchor = WinForms.AnchorStyles.Right,
                BackColor = Drawing.Color.FromArgb(236, 245, 255),
                ForeColor = Drawing.Color.FromArgb(19, 70, 118),
                Font = new Drawing.Font("Segoe UI Semibold", 9F, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Point),
                Padding = new WinForms.Padding(10, 6, 10, 6),
                Text = "Version " + version,
                Margin = new WinForms.Padding(10, 18, 0, 0)
            };

            titleStack.Controls.Add(title, 0, 0);
            titleStack.Controls.Add(subtitle, 0, 1);
            headerLayout.Controls.Add(titleStack, 0, 0);
            headerLayout.Controls.Add(versionBadge, 1, 0);
            headerPanel.Controls.Add(headerLayout);

            return headerPanel;
        }

        private static WinForms.Control CreateBodyPanel(Drawing.Bitmap photo)
        {
            var bodyLayout = new WinForms.TableLayoutPanel
            {
                Dock = WinForms.DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new WinForms.Padding(18, 16, 18, 10),
                BackColor = Drawing.Color.White
            };
            bodyLayout.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Absolute, 270F));
            bodyLayout.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Percent, 100F));
            bodyLayout.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.Percent, 100F));

            bodyLayout.Controls.Add(CreateProfileCard(photo), 0, 0);
            bodyLayout.Controls.Add(CreateDetailsPanel(), 1, 0);
            return bodyLayout;
        }

        private static WinForms.Control CreateProfileCard(Drawing.Bitmap photo)
        {
            var card = new WinForms.Panel
            {
                Dock = WinForms.DockStyle.Fill,
                Padding = new WinForms.Padding(14),
                BackColor = CardBackColor,
                BorderStyle = WinForms.BorderStyle.FixedSingle,
                Margin = new WinForms.Padding(0, 0, 16, 0)
            };

            var layout = new WinForms.TableLayoutPanel
            {
                Dock = WinForms.DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                BackColor = CardBackColor
            };
            layout.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.Absolute, 216F));
            layout.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.AutoSize));
            layout.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.AutoSize));
            layout.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.Percent, 100F));
            layout.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.Absolute, 36F));
            layout.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.Absolute, 8F));
            layout.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.Absolute, 36F));

            var picture = new WinForms.PictureBox
            {
                Dock = WinForms.DockStyle.Fill,
                BorderStyle = WinForms.BorderStyle.FixedSingle,
                SizeMode = WinForms.PictureBoxSizeMode.Zoom,
                BackColor = Drawing.Color.White,
                Image = photo,
                Margin = new WinForms.Padding(0, 0, 0, 10)
            };

            var developerLabel = new WinForms.Label
            {
                AutoSize = true,
                Font = new Drawing.Font("Segoe UI Semibold", 10F, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Point),
                ForeColor = BodyTextColor,
                Text = "Developer: Ajmal P.S.",
                Margin = new WinForms.Padding(0, 0, 0, 2)
            };

            var contactLabel = new WinForms.Label
            {
                AutoSize = true,
                Font = new Drawing.Font("Segoe UI", 9F, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Point),
                ForeColor = MutedTextColor,
                Text = "Support and feature requests are welcome.",
                Margin = new WinForms.Padding(0, 0, 0, 10)
            };

            var linkedInButton = CreateActionButton("LinkedIn Profile", true);
            linkedInButton.Click += (sender, args) => OpenExternal(LinkedInUrl);

            var emailButton = CreateActionButton("Send Email", false);
            emailButton.Click += (sender, args) => OpenExternal("mailto:" + EmailAddress);

            layout.Controls.Add(picture, 0, 0);
            layout.Controls.Add(developerLabel, 0, 1);
            layout.Controls.Add(contactLabel, 0, 2);
            layout.Controls.Add(linkedInButton, 0, 4);
            layout.Controls.Add(emailButton, 0, 6);

            card.Controls.Add(layout);
            return card;
        }

        private static WinForms.Control CreateDetailsPanel()
        {
            var detailsLayout = new WinForms.TableLayoutPanel
            {
                Dock = WinForms.DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new WinForms.Padding(0),
                BackColor = Drawing.Color.White
            };
            detailsLayout.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.AutoSize));
            detailsLayout.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.AutoSize));
            detailsLayout.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.Absolute, 14F));
            detailsLayout.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.AutoSize));
            detailsLayout.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.Percent, 100F));
            detailsLayout.RowStyles.Add(new WinForms.RowStyle(WinForms.SizeType.AutoSize));

            var heading = new WinForms.Label
            {
                AutoSize = true,
                Font = new Drawing.Font("Segoe UI Semibold", 15F, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Point),
                ForeColor = BodyTextColor,
                Text = "Built for Fast, Reliable Revit Production",
                Margin = new WinForms.Padding(0, 0, 0, 8)
            };

            var summary = new WinForms.Label
            {
                AutoSize = true,
                MaximumSize = new Drawing.Size(490, 0),
                Font = new Drawing.Font("Segoe UI", 10F, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Point),
                ForeColor = MutedTextColor,
                Text = "AJ Tools is a practical command suite for Revit 2020 users who want to reduce repetitive work and keep documentation quality consistent across projects.",
                Margin = new WinForms.Padding(0, 0, 0, 0)
            };

            var capabilitiesTitle = new WinForms.Label
            {
                AutoSize = true,
                Font = new Drawing.Font("Segoe UI Semibold", 11F, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Point),
                ForeColor = BodyTextColor,
                Text = "Key Capabilities",
                Margin = new WinForms.Padding(0, 0, 0, 6)
            };

            var capabilitiesText = new WinForms.TextBox
            {
                Dock = WinForms.DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = WinForms.BorderStyle.FixedSingle,
                BackColor = Drawing.Color.White,
                ForeColor = BodyTextColor,
                Font = new Drawing.Font("Segoe UI", 10F, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Point),
                Text =
                    "- Streamline view graphics with quick toggle, apply, match, and reset tools.\r\n" +
                    "- Accelerate documentation with auto dimensions, dim-by-line, and datum reset tools.\r\n" +
                    "- Improve annotation quality using smart MEP tag placement and tag arrangement utilities.\r\n" +
                    "- Navigate linked model data faster with linked ID lookup and search workflows.\r\n" +
                    "- Extend MEP standards with Smart Connect, location data assignment, and duct standards automation.",
                Margin = new WinForms.Padding(0),
                ScrollBars = WinForms.ScrollBars.Vertical,
                TabStop = false
            };

            var supportNote = new WinForms.Label
            {
                AutoSize = true,
                MaximumSize = new Drawing.Size(490, 0),
                Font = new Drawing.Font("Segoe UI", 9F, Drawing.FontStyle.Italic, Drawing.GraphicsUnit.Point),
                ForeColor = MutedTextColor,
                Text = "For support, improvements, or collaboration requests, use the contact options on the left.",
                Margin = new WinForms.Padding(0, 10, 0, 0)
            };

            detailsLayout.Controls.Add(heading, 0, 0);
            detailsLayout.Controls.Add(summary, 0, 1);
            detailsLayout.Controls.Add(capabilitiesTitle, 0, 3);
            detailsLayout.Controls.Add(capabilitiesText, 0, 4);
            detailsLayout.Controls.Add(supportNote, 0, 5);

            return detailsLayout;
        }

        private static WinForms.Control CreateFooterPanel(out WinForms.Button closeButton)
        {
            var footerPanel = new WinForms.Panel
            {
                Dock = WinForms.DockStyle.Bottom,
                Height = 58,
                Padding = new WinForms.Padding(18, 10, 18, 10),
                BackColor = Drawing.Color.White
            };

            footerPanel.Paint += (sender, args) =>
            {
                using (var borderPen = new Drawing.Pen(BorderColor))
                {
                    args.Graphics.DrawLine(borderPen, 0, 0, footerPanel.Width, 0);
                }
            };

            var footerLayout = new WinForms.TableLayoutPanel
            {
                Dock = WinForms.DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Drawing.Color.White
            };
            footerLayout.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Percent, 100F));
            footerLayout.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.AutoSize));

            var copyright = new WinForms.Label
            {
                AutoSize = true,
                Anchor = WinForms.AnchorStyles.Left,
                ForeColor = MutedTextColor,
                Font = new Drawing.Font("Segoe UI", 8.5F, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Point),
                Text = "AJ Tools | Built for dependable daily project delivery"
            };

            closeButton = new WinForms.Button
            {
                AutoSize = true,
                Text = "Close",
                DialogResult = WinForms.DialogResult.OK,
                Font = new Drawing.Font("Segoe UI Semibold", 9F, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Point),
                Padding = new WinForms.Padding(14, 4, 14, 4),
                BackColor = Drawing.Color.FromArgb(238, 243, 249),
                FlatStyle = WinForms.FlatStyle.Flat,
                ForeColor = BodyTextColor,
                Margin = new WinForms.Padding(10, 0, 0, 0)
            };
            closeButton.FlatAppearance.BorderColor = BorderColor;
            closeButton.FlatAppearance.MouseOverBackColor = Drawing.Color.FromArgb(228, 236, 247);
            closeButton.FlatAppearance.MouseDownBackColor = Drawing.Color.FromArgb(218, 228, 241);

            footerLayout.Controls.Add(copyright, 0, 0);
            footerLayout.Controls.Add(closeButton, 1, 0);
            footerPanel.Controls.Add(footerLayout);

            return footerPanel;
        }

        private static WinForms.Button CreateActionButton(string text, bool isPrimary)
        {
            var button = new WinForms.Button
            {
                Dock = WinForms.DockStyle.Fill,
                Text = text,
                Font = new Drawing.Font("Segoe UI Semibold", 9F, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Point),
                FlatStyle = WinForms.FlatStyle.Flat,
                Cursor = WinForms.Cursors.Hand,
                UseVisualStyleBackColor = false
            };

            if (isPrimary)
            {
                button.BackColor = AccentColor;
                button.ForeColor = Drawing.Color.White;
                button.FlatAppearance.BorderColor = AccentColor;
                button.FlatAppearance.MouseOverBackColor = AccentHoverColor;
                button.FlatAppearance.MouseDownBackColor = Drawing.Color.FromArgb(13, 65, 112);
            }
            else
            {
                button.BackColor = Drawing.Color.White;
                button.ForeColor = AccentColor;
                button.FlatAppearance.BorderColor = AccentColor;
                button.FlatAppearance.MouseOverBackColor = Drawing.Color.FromArgb(237, 244, 252);
                button.FlatAppearance.MouseDownBackColor = Drawing.Color.FromArgb(225, 236, 249);
            }

            return button;
        }

        private static string GetVersionString()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null)
            {
                return "Unknown";
            }

            return string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build);
        }

        private static string ResolvePhotoPath()
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            return Path.Combine(assemblyDir, "Resources", OptionalPhotoFileName);
        }

        private static Drawing.Bitmap TryLoadBitmap(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

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

        private static Drawing.Bitmap CreateProfilePlaceholderBitmap()
        {
            var bitmap = new Drawing.Bitmap(320, 320);

            using (var graphics = Drawing.Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var backgroundRect = new Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
                using (var gradient = new System.Drawing.Drawing2D.LinearGradientBrush(
                           backgroundRect,
                           Drawing.Color.FromArgb(234, 241, 250),
                           Drawing.Color.FromArgb(214, 228, 244),
                           45F))
                {
                    graphics.FillRectangle(gradient, backgroundRect);
                }

                using (var borderPen = new Drawing.Pen(Drawing.Color.FromArgb(186, 205, 228), 2F))
                {
                    graphics.DrawRectangle(borderPen, 1, 1, bitmap.Width - 3, bitmap.Height - 3);
                }

                using (var font = new Drawing.Font("Segoe UI Semibold", 108F, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel))
                using (var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(39, 81, 123)))
                using (var sf = new Drawing.StringFormat
                {
                    Alignment = Drawing.StringAlignment.Center,
                    LineAlignment = Drawing.StringAlignment.Center
                })
                {
                    graphics.DrawString("AJ", font, brush, backgroundRect, sf);
                }
            }

            return bitmap;
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
