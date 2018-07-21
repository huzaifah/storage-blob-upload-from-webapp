using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ImageResizeWebApp.Models;
using Microsoft.Extensions.Options;

using System.IO;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using ImageResizeWebApp.Helpers;
using System.Net;
using ImageMagick;
using System.Net.Http.Headers;

namespace ImageResizeWebApp.Controllers
{
    [Route("api/[controller]")]
    public class ImagesController : Controller
    {
        // make sure that appsettings.json is filled with the necessary details of the azure storage
        private readonly AzureStorageConfig storageConfig = null;

        public ImagesController(IOptions<AzureStorageConfig> config)
        {
            storageConfig = config.Value;
        }

        // POST /api/images/upload
        [HttpPost("[action]")]
        public async Task<IActionResult> Upload(ICollection<IFormFile> files)
        {
            bool isUploaded = false;

            try
            {

                if (files.Count == 0)

                    return BadRequest("No files received from the upload");

                if (storageConfig.AccountKey == string.Empty || storageConfig.AccountName == string.Empty)

                    return BadRequest("sorry, can't retrieve your azure storage details from appsettings.js, make sure that you add azure storage details there");

                if (storageConfig.ImageContainer == string.Empty)

                    return BadRequest("Please provide a name for your image container in the azure blob storage");

                foreach (var formFile in files)
                {
                    if (StorageHelper.IsImage(formFile))
                    {
                        if (formFile.Length > 0)
                        {
                            using (Stream stream = formFile.OpenReadStream())
                            {
                                isUploaded = await StorageHelper.UploadFileToStorage(stream, formFile.FileName, storageConfig);
                            }
                        }
                    }
                    else
                    {
                        return new UnsupportedMediaTypeResult();
                    }
                }

                if (isUploaded)
                {
                    if (storageConfig.ThumbnailContainer != string.Empty)

                        return new AcceptedAtActionResult("GetThumbNails", "Images", null, null);

                    else

                        return new AcceptedResult();
                }
                else

                    return BadRequest("Look like the image couldnt upload to the storage");


            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET /api/images/thumbnails
        [HttpGet("thumbnails")]
        public async Task<IActionResult> GetThumbNails()
        {

            try
            {
                if (storageConfig.AccountKey == string.Empty || storageConfig.AccountName == string.Empty)

                    return BadRequest("sorry, can't retrieve your azure storage details from appsettings.js, make sure that you add azure storage details there");

                if (storageConfig.ImageContainer == string.Empty)

                    return BadRequest("Please provide a name for your image container in the azure blob storage");

                List<string> thumbnailUrls = await StorageHelper.GetThumbNailUrls(storageConfig);

                return new ObjectResult(thumbnailUrls);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }

        }

        private string IsURLExists(string url)
        {
            WebRequest webRequest = WebRequest.Create(url);
            webRequest.Timeout = 1200; // miliseconds
            webRequest.Method = "HEAD";

            try
            {
                webRequest.GetResponse();
            }
            catch
            {
                return String.Format("{0}/{1}/{2}", storageConfig.StorageURL, storageConfig.ImageContainer, "small-material.png");
            }

            return url;
        }

        private MagickImage DownloadImageFromUrl(string imageUrl)
        {
            MagickImage image = null;

            try
            {
                imageUrl = IsURLExists(imageUrl);

                System.Net.HttpWebRequest webRequest = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(imageUrl);
                webRequest.AllowWriteStreamBuffering = true;
                webRequest.Timeout = 30000;

                System.Net.WebResponse webResponse = webRequest.GetResponse();

                System.IO.Stream stream = webResponse.GetResponseStream();

                image = new MagickImage(stream);

                webResponse.Close();
            }
            catch (Exception ex)
            {
                return null;
            }

            return image;
        }

        [HttpGet("GetMaterialImage/{materialCode}")]
        public HttpResponseMessage GetMaterialImage(string materialCode)
        {
            string imageUrl = String.Format("{0}/{1}/{2}", storageConfig.StorageURL, storageConfig.ImageContainer, materialCode + ".jpeg");

            MagickImage image = DownloadImageFromUrl(imageUrl);
            HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

            var ms = new MemoryStream();
            result.Content = new StreamContent(ms);

            if (image.Format == MagickFormat.Png)
            {
                image.Write(ms, MagickFormat.Png);
                result.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            }
            else if (image.Format == MagickFormat.Jpeg || image.Format == MagickFormat.Jpg)
            {
                image.Write(ms, MagickFormat.Jpeg);
                result.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            }

            result.Headers.AcceptRanges.Add("bytes");
            result.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("render");
            result.Content.Headers.ContentDisposition.FileName = image.FileName;
            result.Content.Headers.ContentLength = ms.Length;

            return result;

        }

        [HttpGet("GetImageFile/{materialCode}")]
        public IActionResult GetImageFile(string materialCode)
        {
            string imageUrl = String.Format("{0}/{1}/{2}", storageConfig.StorageURL, storageConfig.ImageContainer, materialCode + ".jpeg");

            MagickImage image = DownloadImageFromUrl(imageUrl);

            var ms = new MemoryStream();
            
            if (image.Format == MagickFormat.Png)
            {
                image.Write(ms, MagickFormat.Png);
                return File(ms.ToArray(), "image/png");
            }
            else if (image.Format == MagickFormat.Jpeg || image.Format == MagickFormat.Jpg)
            {
                image.Write(ms, MagickFormat.Jpeg);
                return File(ms.ToArray(), "image/jpeg");
            }
            
            return null;
        }
    }
}