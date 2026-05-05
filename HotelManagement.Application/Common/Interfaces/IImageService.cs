using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HotelManagement.Application.Common.Interfaces;

public interface IImageService
{
    Task<string> SaveImageAsync(Stream imageStream, string fileName, string folder);
    Task DeleteImageAsync(string imageUrl);
}
