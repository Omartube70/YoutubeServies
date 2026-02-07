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
        }

        private void EnsureYoutubeClientInitialized()
        {
            if (_youtube == null)
            {
                _youtube = new YoutubeClient();
            }
        }

        public async Task GetVideoDetailsAsync()
        {
            EnsureYoutubeClientInitialized();

            _videoMetadata = await _youtube.Videos.GetAsync(VideoUrl);
            VideoTitle = _videoMetadata.Title;
            ChannelName = _videoMetadata.Author.ChannelTitle;

            _streamManifest = await _youtube.Videos.Streams.GetManifestAsync(VideoUrl);

            var bestAudio = _streamManifest.GetAudioOnlyStreams()
                .Where(s => s != null)
                .OrderByDescending(s => s.Bitrate)
                .FirstOrDefault();

            var videoStreams = _streamManifest.GetVideoOnlyStreams()
                .Where(s => s != null && s.VideoCodec != null && s.VideoCodec.Contains("avc"))
                .OrderBy(s => s.VideoResolution.Height)
                .GroupBy(s => s.VideoResolution.Height)
                .Select(g => g.OrderByDescending(s => s.Bitrate).First());

            AvailableQualities.Clear();

            foreach (var videoStream in videoStreams)
            {
                long? totalSize = null;

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
            EnsureYoutubeClientInitialized();

            var quality = AvailableQualities.FirstOrDefault(q => q.Height == SelectedQualityHeight);

            if (quality == null)
                throw new Exception("Selected quality not found.");

            if (quality.VideoStream == null)
                throw new Exception("Video stream not found.");

            var progressHandler = new Progress<double>(p =>
            {
                try
                {
                    progress?.Report(p);
                }
                catch { }
            });

            var videoPath = Path.GetTempFileName();
            var audioPath = Path.GetTempFileName();

            try
            {
                await _youtube.Videos.Streams.DownloadAsync(quality.VideoStream, videoPath, progressHandler);

                if (quality.AudioStream != null)
                {
                    await _youtube.Videos.Streams.DownloadAsync(quality.AudioStream, audioPath);

                    await MergeVideoAndAudioAsync(videoPath, audioPath, SavePath);
                }
                else
                {
                    File.Move(videoPath, SavePath);
                }
            }
            finally
            {
                if (File.Exists(videoPath)) File.Delete(videoPath);
                if (File.Exists(audioPath)) File.Delete(audioPath);
            }
        }

        private async Task MergeVideoAndAudioAsync(string videoPath, string audioPath, string outputPath)
        {
            var ffmpegPath = FindFFmpegPath();
            Console.WriteLine($"FFmpeg path: {ffmpegPath}");

            if (string.IsNullOrEmpty(ffmpegPath))
            {
                throw new Exception("FFmpeg not found. Please install FFmpeg or use video-only download.");
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-i \"{videoPath}\" -i \"{audioPath}\" -c copy -y \"{outputPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode != 0)
                {
                    throw new Exception("Failed to merge video and audio.");
                }
            }
        }

        private string FindFFmpegPath()
        {
            var paths = new[]
            {
                "ffmpeg.exe",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"),
                @"C:\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files\ffmpeg\bin\ffmpeg.exe"
            };

            foreach (var path in paths)
            {
                try
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = "-version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };

                    using (var process = System.Diagnostics.Process.Start(startInfo))
                    {
                        process.WaitForExit();
                        if (process.ExitCode == 0)
                        {
                            return path;
                        }
                    }
                }
                catch { }
            }

            return null;
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