using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace API
{
    public static partial class EntryPoint
    {

        [Function("convert")]
        public static HttpResponseData ConvertPost(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "convert/{option1}/{value1}/{option2}/{value2}/{option3}/{value3}")] HttpRequestData req,
            string option1,
            string value1,
            string option2,
            string value2,
            string option3,
            string value3
            )
        {

            try
            {

                //STEP 1
                //get convert options given by caller
                var inputValues = new Dictionary<string, string>();
                //make sure all lower case so easy to parse
                inputValues.Add(option1.ToLower(), value1.ToLower());
                inputValues.Add(option2.ToLower(), value2.ToLower());
                inputValues.Add(option3.ToLower(), value3.ToLower());

                //based on data given by caller, put it nicely in one package
                var convertOptions = new ConvertOptions();
                foreach (var inputOption in inputValues)
                {
                    switch (inputOption.Key)
                    {
                        case "width": convertOptions.WidthPx = int.Parse(inputOption.Value); break;
                        case "height": convertOptions.HeightPx = int.Parse(inputOption.Value); break;
                        case "quality": convertOptions.OutQuality = int.Parse(inputOption.Value); break;
                        case "out": convertOptions.OutFormat = inputOption.Value; break;
                    }
                }

                //STEP 2
                //extract raw image from request
                var rawImage = Tools.ExtractRawImageFromRequest(req);

                //STEP 3
                var convertRequest = new ConvertCommand(convertOptions, rawImage);

                // Function input comes from the request content.
                //prepare image to be converted, makes sure converter can handle the format
                //if invalid image will fail here
                var parsedImage = Tools.PrepareImage(convertRequest.Image, convertRequest.Options.OutQuality);

                //convert image
                var convertedImage = Tools.ConvertImage(parsedImage, convertRequest.Options);

                var responseData = req.CreateResponse(HttpStatusCode.OK);
                responseData.WriteBytes(convertedImage);

                //set based on request format
                //responseData.Headers.Add("content-type", "image/jpeg");

                return responseData;

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

    }
}
