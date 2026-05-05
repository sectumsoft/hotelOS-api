using HotelManagement.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HotelManagement.Infrastructure.Services;

public class LocalImageService : IImageService
{
    private readonly string _webRootPath;

    public LocalImageService(IWebHostEnvironment env)
    {
        _webRootPath = env.WebRootPath
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }

    public async Task<string> SaveImageAsync(Stream imageStream, string fileName, string folder)
    {
        // e.g. wwwroot/uploads/rooms
        var uploadFolder = Path.Combine(_webRootPath, "uploads", folder);
        Directory.CreateDirectory(uploadFolder); // creates if not exists

        // give a unique name to avoid collisions
        var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        var filePath = Path.Combine(uploadFolder, uniqueFileName);

        using var fileStream = new FileStream(filePath, FileMode.Create);
        await imageStream.CopyToAsync(fileStream);

        // return relative URL to store in DB  e.g. /uploads/rooms/abc.jpg
        return $"/uploads/{folder}/{uniqueFileName}";
    }

    public async Task DeleteImageAsync(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return;

        var filePath = Path.Combine(_webRootPath, imageUrl.TrimStart('/'));
        if (File.Exists(filePath))
            File.Delete(filePath);

        await Task.CompletedTask;
    }
}
