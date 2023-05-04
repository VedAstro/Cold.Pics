

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HEIF_Utility
{

    /// <summary>
    /// Simple functions class to convert HEIC to JPG
    /// Note
    /// - uses HUD.dll
    /// - HEIC is supposed to signify a single flat image in an HEIF structure.
    /// TODO rename class
    /// </summary>
    class invoke_dll
    {
        /// <summary>
        /// Call to HUD.dll to convert HIEC to JPG
        /// </summary>
        /// <param name="heif_bin">Binary data of HEIF file</param>
        /// <param name="input_buffer_size">Size of heif_bin</param>
        /// <param name="jpg_quality">Set the quality of the output JPEG image. The value range is 0-100, Higher is better</param>
        /// <param name="ouput_buffer">Where to out put the JPEG image</param>
        /// <param name="output_buffer_size">Size of output_buffer</param>
        /// <param name="temp_filename">Set temporary file name(when this function finish, you can delete this file)</param>
        /// <param name="copysize">the content of this pointer will store the output jpg image's real size</param>
        /// <param name="include_exif">if this == true, then the output jpg image will have the EXIF metadata</param>
        /// <param name="color_profile">if this == true, this function will embed ICC Color Profile to output jpg image</param>
        /// <param name="icc_bin">the binary data of the ICC Profile(Display P3's ICC Profile can be found in here: https://github.com/liuziangexit/EmbedICCProfile/tree/master/icc-profile) . if the parameter "color_profile"==false,icc_bin can be NULL.</param>
        /// <param name="icc_size">size of icc_bin</param>
        [DllImport("HUD.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private unsafe extern static void heif2jpg(byte* heif_bin, int input_buffer_size, int jpg_quality, byte* ouput_buffer, int output_buffer_size, byte* temp_filename, int* copysize, bool include_exif, bool color_profile, byte* icc_bin, int icc_size);

        [DllImport("HUD.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)]
        private unsafe extern static void getexif(byte* heif_bin, int input_buffer_size, byte* ouput_buffer, int output_buffer_size, int* copysize);


        public static byte[] read_heif(Stream rawStream)
        {
            try
            {
                //very important else out put is 0 size
                rawStream.Position = 0;
                System.IO.BinaryReader br = new BinaryReader(rawStream);
                byte[] byte_array = br.ReadBytes((int)rawStream.Length);
                return byte_array;
            }
            catch (Exception ex)
            { throw ex;
            }
        }


        public static unsafe byte[] invoke_heif2jpg(byte[] heif_bin, int jpg_quality, byte[] temp_filename_byte_array, byte[] displayIccProfile, ref int copysize, bool include_exif, bool color_profile)
        {
            if (color_profile == true && displayIccProfile.Length == 1)
                throw new Exception();//没有ICC却指定要写入ICC

            //so that output file can possibly expand 5 times, multiply 5
            var output_buffer = new byte[heif_bin.Length * 5];
            int[] copysize_array = new int[1] { 0 };
            //can we pass temp_filename_byte_array with any array
            fixed (byte* input = &heif_bin[0], output = &output_buffer[0], temp_filename_byte = &temp_filename_byte_array[0], icc_ptr = &displayIccProfile[0])

            fixed (int* copysize_p = &copysize_array[0])
            {
                heif2jpg(input,
                    heif_bin.Length,
                    jpg_quality,
                    output,
                    output_buffer.Length,
                    temp_filename_byte,
                    copysize_p,
                    include_exif,
                    color_profile,
                    icc_ptr,
                    displayIccProfile.Length);
            }
            copysize = copysize_array[0];
            return output_buffer;


        }


        public static unsafe string invoke_getexif(byte[] heif_bin, ref int copysize)
        {
            var output_buffer = new byte[65535];
            int[] copysize_array = new int[1] { 0 };
            fixed (byte* input = &heif_bin[0], output = &output_buffer[0])
            fixed (int* copysize_p = &copysize_array[0])
            {
                getexif(input, heif_bin.Length, output, output_buffer.Length, copysize_p);
            }
            copysize = copysize_array[0];
            return Encoding.Default.GetString(output_buffer, 0, copysize);
        }
    }
}
