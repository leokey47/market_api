using Microsoft.AspNetCore.Mvc;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System.IO;

namespace market_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Ensure only authorized users can upload
    public class CloudinaryController : ControllerBase
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryController(Cloudinary cloudinary)
        {
            _cloudinary = cloudinary;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            // Validate file is an image
            if (!file.ContentType.StartsWith("image/"))
            {
                return BadRequest("Only image files are allowed");
            }

            using (var stream = file.OpenReadStream())
            {
                // Create upload parameters for Cloudinary
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "market-products", // Optional folder name in Cloudinary
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = false
                };

                // Upload to Cloudinary
                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.Error != null)
                {
                    return StatusCode(500, $"Failed to upload image: {uploadResult.Error.Message}");
                }

                // Return the secure URL to the client
                return Ok(new { imageUrl = uploadResult.SecureUrl.ToString() });
            }
        }
    }
}