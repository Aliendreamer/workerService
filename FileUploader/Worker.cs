using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PhotoUploader
{
    public class Worker : BackgroundService
    {
        private readonly string _successStart = "Task Started Successfully";
        private readonly string _taskEnd = "Task stopped at  {0} with total of images uploaded {1}";
        private readonly string directoryKey = "fileDirectory";
        private readonly int MaxTries = 5;
        private readonly int DelayAwait = 300000;
        private List<string> UploadedImages { get;}
        private Queue<string> FailedUploads { get;}
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private  FileSystemWatcher _watcher;
        private  Cloudinary _cloudinary;
        private int _count;

        public Worker(ILogger<Worker> logger,IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            UploadedImages = new List<string>();
            FailedUploads = new Queue<string>();
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(_configuration.GetValue<string>(directoryKey));

            _watcher=new FileSystemWatcher(_configuration.GetValue<string>(directoryKey),"*.jpg");
            _cloudinary = new Cloudinary();
            _logger.LogInformation(_successStart);
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning(string.Join(Environment.NewLine,FailedUploads));
            _logger.LogInformation(string.Format(_taskEnd,DateTime.Now,_count));
            _watcher.Dispose();
            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _watcher.Created +=OnFileCreatedAction;
                bool failed = FailedUploads.Any();
                if (failed)
                {
                    await TryToUploadFailed();
                }
            }
        }

        private async void OnFileCreatedAction(object sender, FileSystemEventArgs e)
        {
            int failed = 0;
            var result = await TryFileUpload(e);
            if (result)
            {
                _count += 1;
                DeleteFileOnPremise();
                return;
            }

            failed += 1;
            if (failed==MaxTries)
            {
                return;
            }
            OnFileCreatedAction(sender, e);

        }
        private async Task< bool> TryFileUpload(FileSystemEventArgs e)
        {
               string filePath = e.FullPath;
               bool success = UploadAction(filePath);
               return await Task.FromResult(success);
        }

        private bool UploadAction(string filePath)
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(filePath),
                Colors = true,
                Backup = true,
                AccessMode = "public",
                UseFilename = true
            };
            var result = _cloudinary.Upload(uploadParams);
            if (result.Error != null)
            {
                FailedUploads.Enqueue(filePath);
                _logger.LogWarning($"Upload failed  for {filePath} at {DateTime.Now.ToLongDateString()}");
                return false;
            }
            UploadedImages.Add(filePath);
            _logger.LogInformation($"Image with id: {result.PublicId} was uploaded at {DateTime.Now.ToLongDateString()}");
            return true;
        }

        private void DeleteFileOnPremise()
        {
            foreach (var image in UploadedImages)
            {
                bool exists = File.Exists(image);
                if (exists)
                {
                    File.Delete(image);
                }
            }
        }

        private async Task TryToUploadFailed()
        {
            await Task.Run(async () =>
            {
                while (FailedUploads.Any())
                {
                    FailedUploads.TryDequeue(out string current);
                    var success = UploadAction(current);
                    if (success)
                    {
                        File.Delete(current);
                    }
                    else
                    {
                        FailedUploads.Enqueue(current);
                    }
                }
                await Task.CompletedTask;
            });
            // awaiting 5 mins before we try again
            Thread.Sleep(DelayAwait);
        }
    }
}
