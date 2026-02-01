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
        public Form1()
        {
            InitializeComponent();
        }

        private clsYoutubeServies _youtubeServies;

        private async void btnCheck_Click(object sender, EventArgs e)
        {
            string VideoUrl = txtVideoLink.Text.Trim();
            bool isvaild = clsValidtion.IsValidUrl(VideoUrl);

            if (!isvaild)
            {
                MessageBox.Show("Invaild! ... Please enter a valid URL.", "Invaild!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _Clear();
                txtVideoLink.Focus();
                return;
            }

            // تعطيل الزر أثناء التحميل
            btnCheck.Enabled = false;
            this.Cursor = Cursors.WaitCursor;

            try
            {
                await _GetVideoDetils(VideoUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            // استدعاء واحد فقط لجلب كل المعلومات
            await _youtubeServies.GetVideoDetailsAsync();

            lblChannelName.Text = _youtubeServies.ChannelName;
            lblVideoName.Text = _youtubeServies.VideoTitle;

            cmbQuality.Items.Clear();

            // إضافة الجودات مع عرض الحجم
            foreach (var q in _youtubeServies.AvailableQualities)
            {
                // يمكنك عرض الحجم مباشرة أو فقط الجودة
                cmbQuality.Items.Add(q.DisplayText); // فقط "720p" مثلاً
                // أو:
                // cmbQuality.Items.Add($"{q.DisplayText} ({q.SizeText})"); // "720p (125.50 MB)"
            }

            if (cmbQuality.Items.Count > 0)
            {
                // اختيار أعلى جودة افتراضياً
                cmbQuality.SelectedIndex = cmbQuality.Items.Count - 1;
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

            // clean file name
            string fileName = lblVideoName.Text;
            string cleanFileName = Regex.Replace(fileName, @"[<>:""/\\|?*]", "");
            if (string.IsNullOrWhiteSpace(cleanFileName))
                cleanFileName = "video";

            // show save file dialog
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
                downloadForm.ShowDialog();
            }
        }

        private void cmbQuality_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbQuality.SelectedIndex < 0 || _youtubeServies == null)
                return;

            // الحصول على الجودة المختارة
            var selectedQuality = _youtubeServies.AvailableQualities[cmbQuality.SelectedIndex];
            _youtubeServies.SelectedQualityHeight = selectedQuality.Height;

            btnDownload.Enabled = true;
        }
    }
}