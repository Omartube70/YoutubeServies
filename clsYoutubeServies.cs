using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace YoutubeServies
{
    public class clsYoutubeServies
    {
        public string VideoUrl { get; set; } // input property

        public string ChannelName { get; private set; } // output property
        public string VideoTitle { get; private set; } // output property
        public List<QualityInfo> AvailableQualities { get; private set; } // output property  

        public int SelectedQualityHeight { get; set; } // input property
        public string SavePath { get; set; } // input property

        // Cache للبيانات
        private YtDlpResult _cachedResult;
        private DateTime _cacheTime;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(10);

        public class QualityInfo
        {
            public int Height { get; set; }
            public double? FileSize { get; set; }
            public string FormatId { get; set; }
            public string DisplayText => $"{Height}p";
            public string SizeText => FileSize.HasValue
                ? $"{(FileSize.Value / (1024 * 1024)):0.00} MB"
                : "Unknown";
        }

        public clsYoutubeServies(string VideoURl)
        {
            AvailableQualities = new List<QualityInfo>();
            this.VideoUrl = VideoURl;
        }

        // دالة واحدة لجلب كل المعلومات مرة واحدة فقط
        public async Task GetVideoDetailsAsync()
        {
            // استخدام الكاش إذا كان موجود
            if (_cachedResult != null && (DateTime.Now - _cacheTime) < _cacheExpiry)
            {
                ProcessCachedData();
                return;
            }

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "yt-dlp.exe",
                // Arguments محسنة لجلب كل الـ formats
                Arguments = $"--no-playlist --skip-download --no-warnings -J {VideoUrl}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using (Process process = new Process { StartInfo = psi })
            {
                process.Start();

                string json = await process.StandardOutput.ReadToEndAsync();

                await Task.Run(() => process.WaitForExit());

                _cachedResult = JsonSerializer.Deserialize<YtDlpResult>(json);
                _cacheTime = DateTime.Now;

                ProcessCachedData();
            }
        }

        private void ProcessCachedData()
        {
            VideoTitle = _cachedResult.title;
            ChannelName = _cachedResult.uploader;

            AvailableQualities.Clear();

            // نفس المنطق القديم - نجيب كل الجودات المتاحة
            var uniqueHeights = _cachedResult.formats
                .Where(f => f.height.HasValue && f.ext == "mp4")
                .GroupBy(f => f.height)
                .OrderBy(g => g.Key)
                .Select(g => g.First());

            foreach (var f in uniqueHeights)
            {
                AvailableQualities.Add(new QualityInfo
                {
                    Height = f.height.Value,
                    FileSize = f.GetSize(),
                    FormatId = f.format_id
                });

                Debug.WriteLine($"Added quality: {f.height}p - Format: {f.ext} - Size: {f.GetSize()} - ID: {f.format_id}");
            }

            Debug.WriteLine($"Total qualities available: {AvailableQualities.Count}");
        }

        // الحصول على حجم الفيديو مباشرة من الكاش
        public double GetVideoSizeByHeight()
        {
            if (_cachedResult == null)
                return -1;

            var quality = AvailableQualities.FirstOrDefault(q => q.Height == SelectedQualityHeight);

            if (quality?.FileSize != null)
            {
                return quality.FileSize.Value / (1024 * 1024);
            }

            return -1;
        }

        // دالة التحميل مع دمج تلقائي
        public async Task DownloadVideoAsync(IProgress<double> progress = null)
        {
            if (_cachedResult == null)
            {
                await GetVideoDetailsAsync();
            }

            var quality = AvailableQualities.FirstOrDefault(q => q.Height == SelectedQualityHeight);

            if (quality == null)
                throw new Exception("Format not found for selected height.");

            // استخدام format selector ذكي - yt-dlp هيختار الأفضل ويدمج تلقائياً
            string formatSelector = $"bestvideo[height<={SelectedQualityHeight}][ext=mp4]+bestaudio[ext=m4a]/best[height<={SelectedQualityHeight}]";

            ProcessStartInfo psiDownload = new ProcessStartInfo
            {
                FileName = "yt-dlp.exe",
                Arguments = $"--no-playlist -f \"{formatSelector}\" --merge-output-format mp4 -o \"{SavePath}\" {VideoUrl}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (Process process = new Process { StartInfo = psiDownload, EnableRaisingEvents = true })
            {
                process.Start();

                while (!process.StandardError.EndOfStream)
                {
                    string line = await process.StandardError.ReadLineAsync();

                    if (line != null && line.Contains("%"))
                    {
                        try
                        {
                            int idx = line.IndexOf("%");
                            string part = line.Substring(0, idx).Trim();
                            string[] split = part.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (split.Length > 0 && double.TryParse(split.Last(), out double value))
                            {
                                progress?.Report(value / 100.0);
                            }
                        }
                        catch
                        {
                            // تجاهل أخطاء parsing
                        }
                    }
                }

                await Task.Run(() => process.WaitForExit());
            }
        }

        // مسح الكاش إذا احتجت
        public void ClearCache()
        {
            _cachedResult = null;
        }
    }
}