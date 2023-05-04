using Microsoft.Azure.Functions.Worker.Http;
using HEIF_Utility;
using Image = SixLabors.ImageSharp.Image;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;

namespace API
{
    public static class Tools
    {
        public static byte[] ExtractRawImageFromRequest(HttpRequestData req)
        {
            var stream = req.Body;

            Console.WriteLine(stream.Length);

            //todo maybe can do some checking here
            var rawStream = StreamToByteArray(stream);

            return rawStream;
        }
        public static async Task<byte[]> ExtractRawImageFromRequest(HttpRequestMessage req)
        {
            //todo maybe can do some checking here
            var rawStream = await req.Content.ReadAsByteArrayAsync();

            return rawStream;
        }

        public static byte[] StreamToByteArray(Stream input)
        {
            //reset stream position
            input.Position = 0;
            MemoryStream ms = new MemoryStream();
            input.CopyTo(ms);
            return ms.ToArray();
        }

        public static byte[] PrepareImage(byte[] rawImageBytes, int jpgOutputQuality)
        {
            using Stream rawStream = new MemoryStream(rawImageBytes);

            //todo needs better file recognition
            try
            {


                //if image info not null, than we know file can be parsed by ImageSharp lib
                //supported formats  Bmp Gif Jpeg Pbm Png Tiff Tga WebP
                rawStream.Position = 0;
                var imageInfo = Image.Identify(rawStream);
                if (imageInfo != null)
                {
                    //LogManager.Info($"TryConvertToJpeg: No conversion needed");
                    return rawImageBytes;
                }
            }
            catch (Exception e)
            {
                //else file is assumed to be HEIC, can be something
                //else that ImageSharp lib doesn't support also
                rawStream.Position = 0;
                var jpgBinary = HeicToJpeg(rawStream, jpgOutputQuality);
                //Stream jpegStream = new MemoryStream(jpgBinary);

                //LogManager.Info($"TryConvertToJpeg: HEIC > JPG {jpgBinary.Length} bytes");

                if (jpgBinary.Length == 0) { throw new Exception("Converted JPG size 0!"); }


                return jpgBinary;
            }


            throw new Exception("END OF THE LINE");

        }

        /// <summary>
        /// Converts a heic stream to jpg
        /// Returns binary array instead of stream to simplify things
        /// </summary>
        public static byte[] HeicToJpeg(Stream heicStream, int jpgOutputQuality)
        {

            var local_root = Environment.GetEnvironmentVariable("AzureWebJobsScriptRoot");
            var azure_root = $"{Environment.GetEnvironmentVariable("HOME")}/site/wwwroot";
            var actual_root = local_root ?? azure_root;


            heicStream.Position = 0;
            var tempheicfile = invoke_dll.read_heif(heicStream);
            int copysize = 0;
            bool has_icc = false;


            //get default icc profile to use
            //note: path gotten from context because in Azure Function this works correctly
            //this file is located in root, but during publish auto goes to wwwroot
            var defaultIccPath = Path.Combine(actual_root, "DisplayP3.icc");

            //NOTE:
            //below line is key to making this work in Production Azure Functions
            //this param is the path to a file where data during
            //conversion is temporarily stored (heic bit stream in c++ by HUD.dll)
            //when running local, a name would do, HUD.dll will try open file first, else create it
            //so in local app directory this works. But In Azure cloud, HUD.dll is denied from
            //creating/editing file in root directory. Thus the use of .NET's temp file, which works well in Azure.
            var tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".hevc");
            //this just converts file name text for HUD.dll to understand
            var tempFilenameByteArray = System.Text.Encoding.Default.GetBytes(tempFilePath);

            //default ICC profile used
            //todo use actual icc data from image
            byte[] defaultIccProfile = System.IO.File.ReadAllBytes(defaultIccPath);

            //do the conversion
            var jpgBinary = invoke_dll.invoke_heif2jpg(
                heif_bin: tempheicfile,
                jpg_quality: jpgOutputQuality,
                temp_filename_byte_array: tempFilenameByteArray,
                displayIccProfile: defaultIccProfile,
                copysize: ref copysize,
                include_exif: true,
                color_profile: true);

            //log it
            Console.WriteLine($"HeicToJpeg  input heic:{heicStream.Length} bytes output jpeg:{jpgBinary.Length}");


            return jpgBinary;
        }

        /// <summary>
        /// Uses image sharp lib to resize and downsample JPEG image stream
        /// also removes EXIF
        /// </summary>
        public static byte[] ConvertImage(byte[] inputBytes, ConvertOptions options)
        {


            //convert to stream
            using var originalImageStream = new MemoryStream(inputBytes);

            originalImageStream.Position = 0;
            var resizedImageStream = new MemoryStream();
            using (var imageToCompress = Image.Load(originalImageStream))
            {
                //remove exif todo can be optional
                imageToCompress.Metadata.ExifProfile = null;
                imageToCompress.Metadata.XmpProfile = null;

                var useOriSize = options.WidthPx == 0 && options.HeightPx == 0;
                if (!useOriSize)
                {
                    //do the resizing, note it's possible to change algorithm if needed
                    //image.Mutate(x => x.Resize(width, height, KnownResamplers.Lanczos3));
                    imageToCompress.Mutate(x => x.Resize(options.WidthPx, options.HeightPx));
                }

                //set compression quality for output
                IImageEncoder encoder = null;

                //set encoder by out format
                switch (options.OutFormat)
                {
                    case "jpg":
                    case "jpeg": encoder = new JpegEncoder { Quality = options.OutQuality }; break;
                    case "bmp": encoder = new BmpEncoder() { }; break;
                    case "gif": encoder = new GifEncoder() { }; break;
                    case "png": encoder = new PngEncoder() { }; break;
                    case "tiff": encoder = new TiffEncoder() { }; break;
                    case "pbm": encoder = new PbmEncoder() { }; break;
                    case "tga": encoder = new TgaEncoder() { }; break;
                    case "webp": encoder = new WebpEncoder() { }; break;

                    default: throw new Exception($"Out format not accounted for : {options.OutFormat}");
                }

                imageToCompress.Save(resizedImageStream, encoder);
                resizedImageStream.Position = 0;
            }
            return StreamToByteArray(resizedImageStream);

        }

    }
}
