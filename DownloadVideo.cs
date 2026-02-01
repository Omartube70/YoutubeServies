using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YoutubeServies
{
    public partial class DownloadVideo : Form
    {
        private clsYoutubeServies _youtubeServies;
        public double Quality { get; private set; }

        public DownloadVideo(clsYoutubeServies youtubeServies)
        {
            InitializeComponent();
            this._youtubeServies = youtubeServies;
        }

        private void DownloadVideo_Load(object sender, EventArgs e)
        {
            lblVideoName.Text = _youtubeServies.VideoTitle;
            lblQuality.Text = _youtubeServies.SelectedQualityHeight.ToString() + "p";

            Quality = _youtubeServies.GetVideoSizeByHeight();

            if (Quality < 0)
            {
                lblSize.Text = "Unknown";
            }
            else
            {
                lblSize.Text = Quality.ToString("0.00") + " MB";
            }

            btnDownload.Enabled = true;
        }

        private async void btnDownload_Click(object sender, EventArgs e)
        {
            
            progressBar1.Value = 0;

            var progressIndicator = new Progress<double>(value =>
            {
                if (value >= 0 && value <= 1)
                {
                    progressBar1.Value = (int)(value * 100);
                }
            });

            btnDownload.Enabled = false;
            this.Cursor = Cursors.WaitCursor;

            try
            {
                await _youtubeServies.DownloadVideoAsync(progressIndicator);
                MessageBox.Show("Download Done!", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnDownload.Enabled = true;
                this.Cursor = Cursors.Default;
            }
        }
    }
}