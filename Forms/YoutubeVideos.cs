using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YoutubeServies
{
    public partial class Form1 : Form
    {
        private clsYoutubeServies _youtubeServies;

        public Form1()
        {
            InitializeComponent();
        }

        private async void btnCheck_Click(object sender, EventArgs e)
        {
            string VideoUrl = txtVideoLink.Text.Trim();
            bool isvaild = clsValidtion.IsValidUrl(VideoUrl);

            if (!isvaild)
            {
                MessageBox.Show("Invalid! Please enter a valid URL.", "Invalid!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _Clear();
                txtVideoLink.Focus();
                return;
            }

            btnCheck.Enabled = false;
            this.Cursor = Cursors.WaitCursor;

            try
            {
                await _GetVideoDetils(VideoUrl);
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;

                if (ex.InnerException is System.Net.Http.HttpRequestException httpEx)
                {
                    if (httpEx.Message.Contains("403"))
                    {
                        errorMessage = "YouTube blocked the request (Error 403).\n\n" +
                            "Possible solutions:\n" +
                            "1. Try a regular YouTube video (not Shorts)\n" +
                            "2. Wait a few minutes and try again\n" +
                            "3. YouTube may have temporary restrictions";
                    }
                }

                MessageBox.Show($"Error: {errorMessage}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _Clear();
            }
            finally
            {
                btnCheck.Enabled = true;
                this.Cursor = Cursors.Default;
            }
        }

        private async Task _GetVideoDetils(string videoUrl)
        {
            _youtubeServies = new clsYoutubeServies(videoUrl);

            await _youtubeServies.GetVideoDetailsAsync();

            lblChannelName.Text = _youtubeServies.ChannelName;
            lblVideoName.Text = _youtubeServies.VideoTitle;

            cmbQuality.Items.Clear();

            foreach (var q in _youtubeServies.AvailableQualities)
            {
                cmbQuality.Items.Add(q.DisplayText);
            }

            if (cmbQuality.Items.Count > 0)
            {
                cmbQuality.SelectedIndex = cmbQuality.Items.Count - 1;
            }
            else
            {
                MessageBox.Show("No video qualities available for this video.",
                    "No Qualities Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void _Clear()
        {
            lblChannelName.Text = "(???)";
            lblVideoName.Text = "(???)";
            cmbQuality.Items.Clear();
            btnDownload.Enabled = false;
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            if (_youtubeServies == null || cmbQuality.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a quality first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string fileName = lblVideoName.Text;
            string cleanFileName = Regex.Replace(fileName, @"[<>:""/\\|?*]", "");
            if (string.IsNullOrWhiteSpace(cleanFileName))
                cleanFileName = "video";

            saveFileDialog1.FileName = cleanFileName + ".mp4";
            saveFileDialog1.Filter = "MP4 Video|*.mp4";
            saveFileDialog1.Title = "Save Video As";

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string SavePath = saveFileDialog1.FileName;
                if (!SavePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                    SavePath += ".mp4";

                _youtubeServies.SavePath = SavePath;

                DownloadVideo downloadForm = new DownloadVideo(_youtubeServies);
                downloadForm.OnDownloadVideoDone += OnDownloadVideoDone;
                downloadForm.ShowDialog();
            }
        }

        private void OnDownloadVideoDone()
        {
            MessageBox.Show("Download completed successfully!", "Download Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _Clear();
        }

        private void cmbQuality_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbQuality.SelectedIndex < 0 || _youtubeServies == null)
                return;

            var selectedQuality = _youtubeServies.AvailableQualities[cmbQuality.SelectedIndex];
            _youtubeServies.SelectedQualityHeight = selectedQuality.Height;

            btnDownload.Enabled = true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}