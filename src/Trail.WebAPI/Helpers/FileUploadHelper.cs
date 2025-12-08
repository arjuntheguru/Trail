using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Trail.WebAPI.Helpers
{
    public static class FileUploadHelper
    {
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

        public static async Task<List<string>> UploadImagesAsync(IFormFileCollection files, IWebHostEnvironment environment, string subFolder = "comments")
        {
            var uploadedFilePaths = new List<string>();

            if (files == null || files.Count == 0)
                return uploadedFilePaths;

            var uploadsFolder = Path.Combine(environment.WebRootPath ?? environment.ContentRootPath, "uploads", subFolder);
            
            // Create directory if it doesn't exist
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            foreach (var file in files)
            {
                if (file == null || file.Length == 0)
                    continue;

                // Validate file extension
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedImageExtensions.Contains(extension))
                    continue;

                // Validate file size
                if (file.Length > MaxFileSize)
                    continue;

                // Generate unique filename
                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Save file
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                // Return relative path for URL
                uploadedFilePaths.Add($"/uploads/{subFolder}/{uniqueFileName}");
            }

            return uploadedFilePaths;
        }
    }
}

