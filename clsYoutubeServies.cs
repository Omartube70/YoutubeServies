using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace YoutubeServies
{
    public class clsYoutubeServies
    {
        public string VideoUrl { get; set; }

        public string ChannelName { get; private set; }
        public string VideoTitle { get; private set; }
        public List<QualityInfo> AvailableQualities { get; private set; }

        public int SelectedQualityHeight { get; set; }
        public string SavePath { get; set; }

        private YoutubeClient _youtube;
        private StreamManifest _streamManifest;
        private YoutubeExplode.Videos.Video _videoMetadata;

        public class QualityInfo
        {
            public int Height { get; set; }
            public long? FileSize { get; set; }
            public IVideoStreamInfo VideoStream { get; set; }
            public IAudioStreamInfo AudioStream { get; set; }

            public string DisplayText => $"{Height}p";
            public string SizeText => FileSize.HasValue
                ? $"{(FileSize.Value / (1024.0 * 1024.0)):0.00} MB"
                : "Unknown";
        }

        public clsYoutubeServies(string videoUrl)
        {
            VideoUrl = videoUrl;
            AvailableQualities = new List<QualityInfo>();
            // لا تهيئة YoutubeClient هنا - سيتم تهيئته عند الحاجة
        }

        private void EnsureYoutubeClientInitialized()
        {
            if (_youtube == null)
            {
                try
                {
                    _youtube = new YoutubeClient();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to initialize YouTube client: {ex.Message}", ex);
                }
            }
        }

        public async Task GetVideoDetailsAsync()
        {
            try
            {
                EnsureYoutubeClientInitialized();

                // جلب معلومات الفيديو
                _videoMetadata = await _youtube.Videos.GetAsync(VideoUrl);
                VideoTitle = _videoMetadata.Title;
                ChannelName = _videoMetadata.Author.ChannelTitle;

                // جلب معلومات الـ streams
                _streamManifest = await _youtube.Videos.Streams.GetManifestAsync(VideoUrl);

                // جلب أفضل صوت
                var bestAudio = _streamManifest.GetAudioOnlyStreams()
                    .Where(s => s != null)
                    .OrderByDescending(s => s.Bitrate)
                    .FirstOrDefault();

                // جلب جميع الجودات المتاحة
                var videoStreams = _streamManifest.GetVideoOnlyStreams()
                    .Where(s => s != null && s.VideoCodec != null && s.VideoCodec.Contains("avc"))
                    .OrderBy(s => s.VideoResolution.Height)
                    .GroupBy(s => s.VideoResolution.Height)
                    .Select(g => g.OrderByDescending(s => s.Bitrate).First());

                AvailableQualities.Clear();

                foreach (var videoStream in videoStreams)
                {
                    long? totalSize = null;

                    // حساب الحجم الكلي (فيديو + صوت)
                    if (videoStream.Size.Bytes > 0)
                    {
                        totalSize = videoStream.Size.Bytes;
                        if (bestAudio != null && bestAudio.Size.Bytes > 0)
                        {
                            totalSize += bestAudio.Size.Bytes;
                        }
                    }

                    AvailableQualities.Add(new QualityInfo
                    {
                        Height = videoStream.VideoResolution.Height,
                        FileSize = totalSize,
                        VideoStream = videoStream,
                        AudioStream = bestAudio
                    });
                }

                if (AvailableQualities.Count == 0)
                {
                    throw new Exception("No compatible video streams found.");
                }
            }
            catch (TypeInitializationException ex)
            {
                throw new Exception($"Initialization error: {ex.Message}\nInner: {ex.InnerException?.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get video details: {ex.Message}", ex);
            }
        }

        public double GetVideoSizeByHeight()
        {
            var quality = AvailableQualities.FirstOrDefault(q => q.Height == SelectedQualityHeight);

            if (quality?.FileSize != null)
            {
                return quality.FileSize.Value / (1024.0 * 1024.0);
            }

            return -1;
        }

        public async Task DownloadVideoAsync(IProgress<double> progress = null)
        {
            try
            {
                EnsureYoutubeClientInitialized();

                var quality = AvailableQualities.FirstOrDefault(q => q.Height == SelectedQualityHeight);

                if (quality == null)
                    throw new Exception("Selected quality not found.");

                if (quality.VideoStream == null)
                    throw new Exception("Video stream not found.");

                // إنشاء Progress wrapper
                var progressHandler = new Progress<double>(p =>
                {
                    try
                    {
                        progress?.Report(p);
                    }
                    catch
                    {
                        // تجاهل أخطاء progress reporting
                    }
                });

                // تحضير قائمة الـ streams
                var streamInfos = new List<IStreamInfo>();
                streamInfos.Add(quality.VideoStream);

                if (quality.AudioStream != null)
                {
                    streamInfos.Add(quality.AudioStream);
                }

                // تحميل ودمج الفيديو والصوت
                await _youtube.Videos.Streams.DownloadAsync(
                    (IStreamInfo)streamInfos,
                    SavePath,
                    progressHandler
                );
            }
            catch (Exception ex)
            {
                throw new Exception($"Download failed: {ex.Message}", ex);
            }
        }

        public void ClearCache()
        {
            _streamManifest = null;
            _videoMetadata = null;
            AvailableQualities.Clear();
        }

        public void Dispose()
        {
            _youtube = null;
            ClearCache();
        }
    }
}