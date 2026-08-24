/*
    FaceSDK C# Wrapper - Platform Invoke Type
    File: FaceSDK.cs

    Remarks:
      250919 minor update

      251017 add api 
       > fsdkc_malloc, fsdkc_free
       > fsdkc_calc_crop_face_area_in_img, fsdkc_crop_face_from_img

      251106 add feature vec extraction and compare
      251217 add PayloadEncrypt RSA(PKCS#8, X509.SKI-PUB)+AES
      260102 add opencv-img-loader, wrapper-api(liveness,occlusion,blink)
      260311 add fineocclusion
      
*/

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using static Alchera.FaceSDK.FaceSDK;

namespace Alchera.FaceSDK
{
    public class FaceSDKException : Exception
    {
        public FaceSDKException(string message)
            : base(message)
        {
        }
    }


    public class FaceSDK
    {
        public class Meta
        {
            public const int VER_MAJ = 1;
            public const int VER_MIN = 3;
            public const int VER_PATCH = 0;
            public const long VER_BUILD = 260311182710;
            public const int VER_FEATURE = 13;  // 0: stable release

            public const int REQ_FSDKC_VER_MAJ = 1;
            public const int REQ_FSDKC_VER_MIN = 4;
            public const int REQ_FSDKC_VER_PATCH = 0;
            public const int REQ_FSDKC_VER_FEATURE = 15; // 0: stable release
        }

        public static readonly string VER_STR = Meta.VER_MAJ + "." + Meta.VER_MIN + "."
            + Meta.VER_PATCH + "-" + Meta.VER_FEATURE + "." + Meta.VER_BUILD;


        public class BlobImg
        {
            public readonly byte[] data_;

            public readonly int width_;
            public readonly int height_;
            public readonly string desc_;  // ex: "bgr-raw", "jpg"

            public BlobImg(byte[] data, int width, int height, string desc = "")
            {
                desc_ = desc;

                if (data != null)
                {
                    data_ = new byte[data.Length];
                    Array.Copy(data, data_, data.Length);
                }

                width_ = width;
                height_ = height;
            }

            public Bitmap MakeBitmap()
            {
                if (data_ == null)
                    return null;

                int w = width_;
                int h = height_;

                if (data_.Length != (w * h * 3))
                    return null;

                Bitmap bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);

                Rectangle rect = new Rectangle(0, 0, w, h);
                BitmapData bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);

                int dstStride = bd.Stride; // gdi, 4byte alignment-pad
                int srcStride = w * 3;

                IntPtr dstScan0 = bd.Scan0;

                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(data_, y * srcStride, dstScan0 + y * dstStride, srcStride);
                }

                bmp.UnlockBits(bd);

                return bmp;
            }

            public static BlobImg MakeBgrBlobImgFromBgrBitmap(Bitmap bgr_bitmap)
            {
                if (bgr_bitmap == null)
                    return null;

                int w = bgr_bitmap.Width;
                int h = bgr_bitmap.Height;

                if (w <= 0 || h <= 0)
                    return null;

                Bitmap bmp = bgr_bitmap;
                Rectangle rect = new Rectangle(0, 0, w, h);
                BitmapData bd = null;
                byte[] bgr = null;

                try
                {
                    bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, bmp.PixelFormat);

                    int src_stride = bd.Stride; // 4byte align-pad
                    if (src_stride == 0)
                        return null;

                    if (src_stride < 0)
                        return null; // bottom-up bitmap (stride < 0)

                    long raw_siz_long = (long)src_stride * h;
                    long bgr_siz_long = (long)w * h * 3;

                    if (raw_siz_long <= 0 || bgr_siz_long <= 0 ||
                        raw_siz_long > int.MaxValue || bgr_siz_long > int.MaxValue)
                    {
                        return null; // image is too large or corrupted
                    }

                    int raw_size = (int)raw_siz_long;
                    int bgr_size = (int)bgr_siz_long;

                    byte[] raw = new byte[raw_size];
                    bgr = new byte[bgr_size];

                    Marshal.Copy(bd.Scan0, raw, 0, raw.Length);

                    int dst_stride = w * 3;
                    int dst_index = 0;

                    // remove pad
                    if (bmp.PixelFormat == PixelFormat.Format24bppRgb)
                    {
                        for (int y = 0; y < h; y++)
                        {
                            int src_index = y * src_stride;
                            Buffer.BlockCopy(raw, src_index, bgr, dst_index, dst_stride);
                            dst_index += dst_stride;
                        }
                    }
                    else if (bmp.PixelFormat == PixelFormat.Format32bppArgb ||
                            bmp.PixelFormat == PixelFormat.Format32bppPArgb ||
                            bmp.PixelFormat == PixelFormat.Format32bppRgb)
                    {
                        for (int y = 0; y < h; y++)
                        {
                            int src_index = y * src_stride;
                            for (int x = 0; x < w; x++)
                            {
                                int i = src_index + x * 4;
                                int di = dst_index + x * 3;
                                bgr[di + 0] = raw[i + 0];
                                bgr[di + 1] = raw[i + 1];
                                bgr[di + 2] = raw[i + 2];
                            }
                            dst_index += dst_stride;
                        }
                    }
                    else
                    {
                        /*
                          Bitmap converted = new Bitmap(bmp);
                          using (Graphics g = Graphics.FromImage(converted))
                          {                            
                              // GDI+ may perform implicit color conversions during DrawImage()
                              // DrawImage() is NOT a guaranteed pixel-perfect copy.
                              // It may alter colors depending on the source PixelFormat and rendering path. 
                              g.DrawImage(bmp, 0, 0); // 
                          }
                         */
                        return null; // unsupported pixel format
                    }
                }
                finally
                {
                    if (bd != null)
                        bmp.UnlockBits(bd);
                }

                if (bgr == null)
                    return null;

                return new BlobImg(bgr, w, h);
            }            
        }


        public class Params
        {
            //
            // threshold
            //
            public const float THRESHOLD_ATTR_FACE_IS_MASKED = 0.5F;
            public const float THRESHOLD_FEATURE = 0.2630F; // G-2

            //
            // crop face
            //

            // [0] for file img (galley photo), compare
            // [1] for detected face on front camera(android,ios)
            public static readonly float[] CROPFACE_WH_MARGION_RATIO = { 0.25f, 0.33f };
            public static readonly int[] CROPFACE_RESIZE_WIDTH_AFTER_CROP = { 150, 200 };
        }

        //
        // Native (FaceSDKCS.dll)
        //

        private class Native
        {
            public const int FSDKC_RST_ERR_DESC_MAX = 256;

            public class ErrDesc
            {
                public ErrDesc()
                {
                    siz_ = FSDKC_RST_ERR_DESC_MAX;
                    data_ = new byte[siz_];
                }

                public string GetErrStr()
                {
                    return Encoding.ASCII.GetString(data_).TrimEnd('\0');
                }

                public byte[] data_;
                public uint siz_;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
            public struct fsdkc_rst
            {
                public int last_err;

                public int img1_face_cnt;
                public Face img1_face;

                public int img2_face_cnt;
                public Face img2_face;

                // feature 
                public float face_feature_quality;
                public float face_features_dist;        // feature dist (img1_face, img2_face)

                // attribute
                public float face_mask_confidence;

                // antispoofing
                public float img_face_quality_for_liveness;
                public float img_liveness;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
            public struct fsdkc_crop_face_area_in_img_rst
            {
                public int last_err;

                public int x;
                public int y;
                public int w;
                public int h;

                public float margin_ratio;

                public int resize_w; // 0 for no resize
                public int resize_h; // 0 for no resize
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
            public struct fsdkc_crop_face_rst
            {
                public int last_err;

                public int face_w;
                public int face_h;

                public uint face_bgr_buf_siz;  // fsdk_siz_t is always uint

                // must be freed by fsdkc_free() from caller(c#, ..)
                public IntPtr _face_bgr_buf__need_nat_free;
            };

            [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
            public struct fsdkc_bool_rst
            {
                public int last_err;
                public int false0_true1_rst;  // 0: false,  1: true
            };

            [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
            public struct fsdkc_face_occlusion_rst
            {
                public int last_err;

                public float left_eye_confidence;
                public float right_eye_confidence;
                public float mouth_confidence;
            };

            [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
            public struct fsdkc_eye_state_rst
            {
                public int last_err;

                public float left_eyelid_distance;
                public float right_eyelid_distance;
            };

            [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
            public struct fsdkc_confidence_rst
            {
                public int last_err;

                public float confidence;   // 0.0 ~ 1.0
            };


            //
            // Init/DeInit
            //

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_rst fsdkc_initialize(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                String mdl_path, String sdk_path);

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_rst fsdkc_deinitialize(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz);


            //
            // Detect
            //

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_rst fsdkc_detect_one_face(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] img1_bgr_buf, uint img1_bgr_buf_siz, int img1_w, int img1_h,
                int use_continuous_img_detect);


            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_confidence_rst fsdkc_compute_landmark_confidence(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] img_bgr_buf, uint img_bgr_buf_siz, int img_w, int img_h,
                [In] ref Face face);

            //
            // Feature 
            //

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_rst fsdkc_feature_compute_dist_from_imgs(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] img1_bgr_buf,
                uint img1_bgr_buf_siz, int img1_w, int img1_h,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 7)] byte[] img2_bgr_buf,
                uint img2_bgr_buf_siz, int img2_w, int img2_h);

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_rst fsdkc_feature_estimate_face_quality(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] img1_bgr_buf,
                uint img1_bgr_buf_siz, int img1_w, int img1_h,
                [In] ref Face face, int is_face_masked);

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_rst fsdkc_feature_extract_feature_vector_from_img(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] float[] feature_vec, uint feature_vec_siz /* == 512 */,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)] byte[] img1_bgr_buf,
                uint img1_bgr_buf_siz, int img1_w, int img1_h,
                [In] ref Face face);

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_rst fsdkc_feature_compute_feature_vector_dist(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] float[] in_feature_vec1, uint in_feature_vec1_cnt /* == 512 */,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)] float[] in_feature_vec2, uint in_feature_vec2_cnt /* == 512 */
                );


            //
            // Attirbute
            //

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_rst fsdkc_attr_check_mask(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] img1_bgr_buf, uint img1_bgr_buf_siz, int img1_w, int img1_h,
                [In] ref Face face);

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_face_occlusion_rst fsdkc_attr_detect_face_occlusion(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] img_bgr_buf, uint img_bgr_buf_siz, int img_w, int img_h,
                [In] ref Face face);

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_confidence_rst fsdkc_attr_detect_fine_occlusion(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] img_bgr_buf, uint img_bgr_buf_siz, int img_w, int img_h,
                [In] ref Face face);


            //
            // AntiSpoofing
            //

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_rst fsdkc_antisp_estimate_face_quality(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] img1_bgr_buf, uint img1_bgr_buf_siz, int img1_w, int img1_h,
                [In] ref Face face);


            // You have to call this func at least >> four(4) times << to get accurate results.
            // > If you input new face info after call 4 times, you must call
            // > fsdkc_antisp_reset_check_liveness() to delete cache
            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_confidence_rst fsdkc_antisp_check_liveness(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] img1_bgr_buf, uint img1_bgr_buf_siz, int img1_w, int img1_h,
                [In] ref Face face);

            // true(1): cache is deleted(reset) , false(0): failed
            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_bool_rst fsdkc_antisp_reset_check_liveness(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz);

            // // You have to call with at least four continues image to get accurate bgr liveness result
            // You must input `face` parameter as detected face information of `img4`
            // > This API doesn't need to manage cache
            // > so you don't need to care calling `fsdkc_antisp_reset_check_liveness()`
            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_confidence_rst fsdkc_antisp_check_liveness_with_multi_imgs(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] img1_bgr_buf, uint img1_bgr_buf_siz, int img1_w, int img1_h,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 7)] byte[] img2_bgr_buf, uint img2_bgr_buf_siz, int img2_w, int img2_h,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 11)] byte[] img3_bgr_buf, uint img3_bgr_buf_siz, int img3_w, int img3_h,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 15)] byte[] img4_bgr_buf, uint img4_bgr_buf_siz, int img4_w, int img4_h,
                [In] ref Face face);

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_eye_state_rst fsdkc_antisp_detect_closed_eyes(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] img1_bgr_buf, uint img1_bgr_buf_siz, int img1_w, int img1_h,
                [In] ref Face face);


            //
            // Util
            //

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_crop_face_area_in_img_rst fsdkc_calc_crop_face_area_in_img(
                int src_img_w, int src_img_h, float face_x, float face_y, float face_w, float face_h,
                float crop_face_w_h_margin_ratio, /* 1.0 for no margin */
                int resize_width_after_crop /* 0 for no resize */
            );

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_crop_face_rst fsdkc_crop_face_from_img(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] img_bgr_buf, uint img_bgr_buf_siz, int img_w, int img_h,
                int crop_x, int crop_y, int crop_w, int crop_h,
                int resize_w, int resize_h /* 0 for no resize */
            );


            //
            // Etc
            //

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern IntPtr fsdkc_get_ver();

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern IntPtr fsdkc_to_err_str(int err_code);

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern IntPtr fsdkc_malloc(uint siz); /* always uint at 32bit/64bit arch */

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern void fsdkc_free(IntPtr m);
        }

        //
        // Wrapper
        //

        public enum Error : int
        {
            NoError,
            NotInitialized,
            CanNotReadModel,
            InvalidLicense,
            LicenseExpired,
            UnknownLicenseError,
            CanNotOpenExtensionFile,
            InvalidExtension,
            DisabledExtension,
            NotPermittedInThisLicense,
            NothingAtInput,
            ModelIsNotInitialized,
            CanNotSetGPUIDAfterInitialization,
            CanNotSetGPUIDWithoutGPU,
            CanNotReadLicense,
            SystemTimeTampered,
            LicenseNotStarted,
            SystemFunctionError
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        public struct Point
        {
            public float x;
            public float y;
        };


        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        public struct Box
        {
            public float x;
            public float y;
            public float w;
            public float h;
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        public struct Pose
        {
            public float yaw;
            public float pitch;
            public float roll;
        };

        // mandatory: attribute/feature extension
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        public struct LandMark
        {
            public static readonly int RAW_SIZE = Marshal.SizeOf(typeof(LandMark));
            private static readonly System.Reflection.FieldInfo[] FIELDS_CACHE =
                typeof(LandMark).GetFields(System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance);

            public const int PT_CNT = 106;
            // 4.x .net framework does not support 'unsafe fixed Point[106]'
            public Point p0, p1, p2, p3, p4, p5, p6, p7, p8, p9,
                         p10, p11, p12, p13, p14, p15, p16, p17, p18, p19,
                         p20, p21, p22, p23, p24, p25, p26, p27, p28, p29,
                         p30, p31, p32, p33, p34, p35, p36, p37, p38, p39,
                         p40, p41, p42, p43, p44, p45, p46, p47, p48, p49,
                         p50, p51, p52, p53, p54, p55, p56, p57, p58, p59,
                         p60, p61, p62, p63, p64, p65, p66, p67, p68, p69,
                         p70, p71, p72, p73, p74, p75, p76, p77, p78, p79,
                         p80, p81, p82, p83, p84, p85, p86, p87, p88, p89,
                         p90, p91, p92, p93, p94, p95, p96, p97, p98, p99,
                         p100, p101, p102, p103, p104, p105;

            // p0~p105 can be compacted or moved by GC, 
            // > so It may not be in a contiguous memory layout
            // > when calling the C++ 'pi' function, you must pass the data in an array form.
            public Point[] ToArray()
            {
                Point[] pts = new Point[PT_CNT];

                for (int i = 0; i < PT_CNT; i++)
                {
                    pts[i] = (Point)FIELDS_CACHE[i].GetValue(this);
                }

                return pts;
            }
        };

        //LandMark NewLandMark()
        //{
        //    return new LandMark();
        //}

        // mandatory: attribute/feature extension
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        public struct LandMark5pt
        {
            public static readonly int RAW_SIZE = Marshal.SizeOf(typeof(LandMark5pt));
            private static readonly System.Reflection.FieldInfo[] FIELDS_CACHE =
                typeof(LandMark5pt).GetFields(System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance);

            public const int PT_CNT = 5;
            // 4.x .net framework does not support 'unsafe fixed Point[5]'
            public Point p0, p1, p2, p3, p4; // for array interop from native

            // p0~p4 can be compacted or moved by GC
            // > so It may not be in a contiguous memory layout
            // > when calling the C++ 'pi' function, you must pass the data in an array form.
            public Point[] ToArray()
            {
                Point[] pts = new Point[PT_CNT];

                for (int i = 0; i < PT_CNT; i++)
                {
                    pts[i] = (Point)FIELDS_CACHE[i].GetValue(this);
                }

                return pts;
            }
        };

        //LandMark5pt NewLandMark5pt()
        //{
        //    return new LandMark5pt();
        //}

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        public struct Face
        {
            public int id; // -1
            public Box box;
            public LandMark landmark;
            public LandMark5pt landmark_5pt;
            public Pose pose;
        };

        public struct FeatureVec
        {
            public const int DIM_CNT = 512;
            public float[] vec; /* float[512], 512-dimensional face vector*/

            public bool IsErr() { return (vec == null || vec.Length < DIM_CNT) ? true : false; }

            public FeatureVec(bool init = true)
            {
                vec = new float[DIM_CNT];
            }

            public string ToBase64()
            {
                if (IsErr())
                    return "";

                byte[] bytes = new byte[vec.Length * sizeof(float)];
                Buffer.BlockCopy(vec, 0, bytes, 0, bytes.Length);

                return Convert.ToBase64String(bytes);
            }
        }


        public struct Rst
        {
            public Error last_err;                  // 0 for ok
            public string last_err_desc;

            public int face_cnt;                    // 0: not detected
            public Face face;

            // extra
            public int face2_cnt;
            public Face face2;

            // detect
            public float face_landmark_confidence;

            // feature 
            public float face_feature_quality;
            public float feature_vec_distance;

            // attribute
            public float face_mask_confidence;

            public float face_occlu_conf_left_eye;
            public float face_occlu_conf_right_eye;
            public float face_occlu_conf_mouth;

            public float face_fine_occlu_score;


            // antispoofing
            public float img_liveness;
            public float img_face_quality_for_liveness;

            public float img_face_eye_state_left_eyelid_dist;
            public float img_face_eye_state_right_eyelid_dist;


            public int GetFaceCnt()
            {
                return face_cnt;
            }

            public int GetExtraFaceCnt()
            {
                return face2_cnt;
            }

            public bool IsFaceDetected()
            {
                return face_cnt == 0 ? false : true;
            }

            public Face GetFace()
            {
                return face;
            }

            public Face GetExtraFace()
            {
                return face2;
            }

            public bool IsOk()
            {
                return last_err == Error.NoError ? true : false;
            }

            public bool IsErr()
            {
                return last_err == Error.NoError ? false : true;
            }

            public Error GetLastErr()
            {
                return last_err;
            }

            public string GetLastErrStr()
            {
                return last_err.ToString();
            }

            public string GetLastErrDesc()
            {
                return last_err_desc;
            }

            public float GetFeatureDist()
            {
                return feature_vec_distance;
            }

            public float GetImgLiveness()
            {
                return img_liveness;
            }

            public bool IsFaceMasked()
            {
                return face_mask_confidence < FaceSDK.Params.THRESHOLD_ATTR_FACE_IS_MASKED;
            }

            public float GetFaceQualityForFeature()
            {
                return face_feature_quality;
            }

            public float GetFaceQualityForLiveness()
            {
                return img_face_quality_for_liveness;
            }
        };


        public struct RstFeatureVec
        {
            public Error last_err;        // 0 for ok
            public string last_err_desc;

            public FeatureVec feature_vec;    // float[512], 512-dimensional face vector

            public bool IsErr() {
                if (last_err != 0)
                    return true;

                if (feature_vec.IsErr())
                    return true;

                return false;
            }

            public string ToBase64()
            {
                if (IsErr())
                    return "";

                return feature_vec.ToBase64();
            }
        }



        static public void Print(Pose pose, String prefix = "")
        {
            Console.WriteLine(prefix + " face-pose: yaw="
                + pose.yaw + " pitch=" + pose.pitch + " roll=" + pose.roll);
        }

        static public void Print(Box box, String prefix = "")
        {
            Console.WriteLine(prefix + " face-box: x=" + box.x
                + " y=" + box.y + " w=" + box.w + " h=" + box.h);
        }

        static public void Print(Face face, String prefix = "")
        {
            //Console.WriteLine(prefix + " face-id=" + face.id);
            Print(face.pose, prefix);
            Print(face.box, prefix);
        }


        private Rst NewRst(Error last_err, string last_err_desc)
        {
            Rst r = new Rst();

            r.last_err = last_err;
            r.last_err_desc = last_err_desc;

            return r;
        }

        private Rst NewRst(ref Native.fsdkc_rst nrst, Native.ErrDesc err_desc)
        {
            Rst r = new Rst();

            r.last_err = (Error)nrst.last_err;
            r.last_err_desc = err_desc.GetErrStr();

            return r;
        }

        private Rst NewRst(ref Native.fsdkc_confidence_rst nrst, Native.ErrDesc err_desc)
        {
            Rst r = new Rst();

            r.last_err = (Error)nrst.last_err;
            r.last_err_desc = err_desc.GetErrStr();

            return r;
        }

        private Rst NewRst(ref Native.fsdkc_face_occlusion_rst nrst, Native.ErrDesc err_desc)
        {
            Rst r = new Rst();

            r.last_err = (Error)nrst.last_err;
            r.last_err_desc = err_desc.GetErrStr();

            if (r.IsOk())
            {
                r.face_occlu_conf_left_eye = nrst.left_eye_confidence;
                r.face_occlu_conf_right_eye = nrst.right_eye_confidence;
                r.face_occlu_conf_mouth = nrst.mouth_confidence;
            }

            return r;
        }

        private Rst NewRst(ref Native.fsdkc_eye_state_rst nrst, Native.ErrDesc err_desc)
        {
            Rst r = new Rst();

            r.last_err = (Error)nrst.last_err;
            r.last_err_desc = err_desc.GetErrStr();

            if (r.IsOk())
            {
                r.img_face_eye_state_left_eyelid_dist = nrst.left_eyelid_distance;
                r.img_face_eye_state_right_eyelid_dist = nrst.right_eyelid_distance;
            }

            return r;
        }


        public struct BoolRst
        {
            public Error last_err;                  // 0 for ok
            public string last_err_desc;

            public bool result;

            public bool GetResult()
            {
                return result;
            }

            public bool IsOk()
            {
                return last_err == Error.NoError ? true : false;
            }

            public bool IsErr()
            {
                return last_err == Error.NoError ? false : true;
            }

            public Error GetLastErr()
            {
                return last_err;
            }

            public string GetLastErrStr()
            {
                return last_err.ToString();
            }

            public string GetLastErrDesc()
            {
                return last_err_desc;
            }
        }

        private BoolRst NewBoolRst(ref Native.fsdkc_bool_rst nrst, Native.ErrDesc err_desc)
        {
            BoolRst r = new BoolRst();

            r.last_err = (Error)nrst.last_err;
            r.last_err_desc = err_desc.GetErrStr();

            if (r.IsOk())
            {
                r.result = nrst.false0_true1_rst == 1 ? true : false;
            }
            else
            {
                r.result = false;
            }

            return r;
        }


        //
        // util
        //

        public static int GetByteSiz(float[] ary)
        {
            if (ary == null)
                return 0;

            return sizeof(float) * ary.Length;
        }

        public static string GetFSDKCVer()
        {
            return Marshal.PtrToStringAnsi(Native.fsdkc_get_ver());
        }

        //
        // Wrapper
        //

        private static FaceSDK instance_;
        private static readonly object single_lock_ = new object();

        private FaceSDK() { }

        public static FaceSDK Instance()
        {
            if (instance_ == null)
            {
                lock (single_lock_)
                {
                    if (instance_ == null)
                    {
                        instance_ = new FaceSDK();
                    }
                }
            }
            return instance_;
        }

        public static void DestroyInstance()
        {
            if (instance_ == null)
                return;

            lock (single_lock_)
            {
                instance_.DeInitialize();
                instance_ = null;
            }
        }


        public Rst Initialize(string mdl_path, string sdk_path)
        {
            Native.ErrDesc err_desc = new Native.ErrDesc();
            Native.fsdkc_rst nrst = Native.fsdkc_initialize(err_desc.data_, err_desc.siz_, mdl_path, sdk_path);

            return NewRst(ref nrst, err_desc);
        }

        public Rst DeInitialize()
        {
            Native.ErrDesc err_desc = new Native.ErrDesc();
            Native.fsdkc_rst nrst = Native.fsdkc_deinitialize(err_desc.data_, err_desc.siz_);

            return NewRst(ref nrst, err_desc);
        }

        //
        // detect
        //

        public Rst DetectFace(byte[] src1_bgr, int src1_w, int src1_h, bool use_continuous_img_detect = false)
        {
            Native.ErrDesc err_desc = new Native.ErrDesc();

            Native.fsdkc_rst nrst = Native.fsdkc_detect_one_face(
                err_desc.data_, err_desc.siz_,
                src1_bgr, (uint)src1_bgr.Length, src1_w, src1_h,
                use_continuous_img_detect ? 1 : 0);

            Rst r = NewRst(ref nrst, err_desc);

            if (r.IsOk())
            {
                if (nrst.img1_face_cnt > 0)
                {
                    r.face_cnt = nrst.img1_face_cnt;

                    // face[0]
                    r.face = nrst.img1_face;
                }
            }

            return r;
        }

        public Rst ComputeLandmarkConfidence(byte[] src1_bgr, int src1_w, int src1_h, ref Face face)
        {
            Native.ErrDesc err_desc = new Native.ErrDesc();

            Native.fsdkc_confidence_rst nrst = Native.fsdkc_compute_landmark_confidence(
                err_desc.data_, err_desc.siz_,
                src1_bgr, (uint)(src1_bgr.Length), src1_w, src1_h,
                ref face);

            Rst r = NewRst(ref nrst, err_desc);

            if (r.IsOk())
            {
                r.face_landmark_confidence = nrst.confidence;
            }

            return r;
        }


        //
        // Feature 
        //

        public Rst EstimateFaceQuality(byte[] src1_bgr, int src1_w, int src1_h, ref Face face, bool is_face_masked)
        {
            Native.ErrDesc err_desc = new Native.ErrDesc();
            Native.fsdkc_rst nrst = Native.fsdkc_feature_estimate_face_quality(
                err_desc.data_, err_desc.siz_,
                src1_bgr, (uint)(src1_bgr.Length), src1_w, src1_h,
                ref face, is_face_masked ? 1 : 0);

            Rst r = NewRst(ref nrst, err_desc);

            r.face_feature_quality = nrst.face_feature_quality;

            return r;
        }


        public Rst CompareImg(byte[] src1_bgr, int src1_w, int src1_h,
            byte[] src2_bgr, int src2_w, int src2_h)
        {
            Native.ErrDesc err_desc = new Native.ErrDesc();

            Native.fsdkc_rst nrst = Native.fsdkc_feature_compute_dist_from_imgs(
                err_desc.data_, err_desc.siz_,
                src1_bgr, (uint)(src1_bgr.Length), src1_w, src1_h,
                src2_bgr, (uint)(src2_bgr.Length), src2_w, src2_h);

            Rst r = NewRst(ref nrst, err_desc);

            if (r.IsOk())
            {
                r.feature_vec_distance = nrst.face_features_dist;
                r.face_cnt = nrst.img1_face_cnt;

                if (nrst.img1_face_cnt > 0)
                {
                    r.face = nrst.img1_face;
                }

                r.face2_cnt = nrst.img2_face_cnt;

                if (nrst.img2_face_cnt > 0)
                {
                    r.face2 = nrst.img2_face;
                }
            }

            return r;
        }


        public RstFeatureVec ExtractFeatureVecFromImg(byte[] src_bgr, int src_w, int src_h,
            bool use_continuous_img_detect = false)
        {
            RstFeatureVec rst_feature = new RstFeatureVec();

            Rst rst_detect = DetectFace(src_bgr, src_w, src_h);

            if (rst_detect.IsErr()) {
                rst_feature.last_err = rst_detect.last_err;
                rst_feature.last_err_desc = rst_detect.last_err_desc;

                return rst_feature;
            }

            Native.ErrDesc err_desc = new Native.ErrDesc();

            FeatureVec feature_vec = new FeatureVec(true);

            Native.fsdkc_rst nrst = Native.fsdkc_feature_extract_feature_vector_from_img(
                err_desc.data_, err_desc.siz_,
                feature_vec.vec, (uint)feature_vec.vec.Length,
                src_bgr, (uint)src_bgr.Length, src_w, src_h,
                ref rst_detect.face);

            Rst r = NewRst(ref nrst, err_desc);

            rst_feature.last_err = r.last_err;
            rst_feature.last_err_desc = r.last_err_desc;

            if (r.IsOk())
            {
                rst_feature.feature_vec = feature_vec;
            }

            return rst_feature;
        }


        public Rst CompareFeatureVecs(FeatureVec feature_vec1, FeatureVec feature_vec2)
        {
            if (feature_vec1.IsErr() || feature_vec2.IsErr())
            {
                return new Rst {
                    last_err = Error.NothingAtInput,
                    last_err_desc = "Invalid Feature Vector Input!!"
                };
            }

            Native.ErrDesc err_desc = new Native.ErrDesc();
            Native.fsdkc_rst nrst = Native.fsdkc_feature_compute_feature_vector_dist(
                err_desc.data_, err_desc.siz_,
                feature_vec1.vec, (uint)feature_vec1.vec.Length,
                feature_vec2.vec, (uint)feature_vec2.vec.Length);

            Rst r = NewRst(ref nrst, err_desc);

            if (r.IsOk())
            {
                r.feature_vec_distance = nrst.face_features_dist;
            }

            return r;
        }

        //
        // Antispoofing
        //

        public Rst GetImgFaceQualityForLiveness(byte[] src1_bgr, int src1_w, int src1_h, ref Face face)
        {
            Native.ErrDesc err_desc = new Native.ErrDesc();

            Native.fsdkc_rst nrst = Native.fsdkc_antisp_estimate_face_quality(
                err_desc.data_, err_desc.siz_,
                src1_bgr, (uint)(src1_bgr.Length), src1_w, src1_h,
                ref face);

            Rst r = NewRst(ref nrst, err_desc);

            if (r.IsOk())
            {
                r.img_face_quality_for_liveness = nrst.img_face_quality_for_liveness;
            }

            return r;
        }

        // You have to call at least four times to get accurate results.
        // If you input new face info after call 'GetImgLiveness' 4 times,
        // you must call ResetImgLivenessCheck() to delete cache! <
        public Rst GetImgLiveness(byte[] src1_bgr, int src1_w, int src1_h, ref Face face)
        {
            Native.ErrDesc err_desc = new Native.ErrDesc();

            Native.fsdkc_confidence_rst nrst = Native.fsdkc_antisp_check_liveness(
                err_desc.data_, err_desc.siz_,
                src1_bgr, (uint)(src1_bgr.Length), src1_w, src1_h,
                ref face);

            Rst r = NewRst(ref nrst, err_desc);

            if (r.IsOk())
            {
                r.img_liveness = nrst.confidence;
            }

            return r;
        }

        public BoolRst ResetImgLivenessCheck()
        {
            Native.ErrDesc err_desc = new Native.ErrDesc();

            Native.fsdkc_bool_rst nrst = Native.fsdkc_antisp_reset_check_liveness(
                err_desc.data_, err_desc.siz_);

            BoolRst r = NewBoolRst(ref nrst, err_desc);

            return r;
        }


        // You have to call with at least four continues image to get accurate bgr liveness result
        // You must input `face` parameter as detected face information of `img4`
        // > This API doesn't need to manage cache
        // > so you don't need to care calling `ResetImgLivenessCheck()`
        public Rst GetImgLivenessMulti(byte[] src1_bgr, int src1_w, int src1_h,
            byte[] src2_bgr, int src2_w, int src2_h, byte[] src3_bgr, int src3_w, int src3_h,
            byte[] src4_bgr, int src4_w, int src4_h, ref Face face)
        {
            Native.ErrDesc err_desc = new Native.ErrDesc();

            Native.fsdkc_confidence_rst nrst = Native.fsdkc_antisp_check_liveness_with_multi_imgs(
                err_desc.data_, err_desc.siz_,
                src1_bgr, (uint)(src1_bgr.Length), src1_w, src1_h,
                src2_bgr, (uint)(src2_bgr.Length), src2_w, src2_h,
                src3_bgr, (uint)(src3_bgr.Length), src3_w, src3_h,
                src4_bgr, (uint)(src4_bgr.Length), src4_w, src4_h,
                ref face);

            Rst r = NewRst(ref nrst, err_desc);

            if (r.IsOk())
            {
                r.img_liveness = nrst.confidence;
            }

            return r;
        }

        //  Extract the closed eyes from the faces.
        public Rst DetectClosedEyes(byte[] src1_bgr, int src1_w, int src1_h, ref Face face)
        {
            Native.ErrDesc err_desc = new Native.ErrDesc();

            Native.fsdkc_eye_state_rst nrst = Native.fsdkc_antisp_detect_closed_eyes(
                err_desc.data_, err_desc.siz_,
                src1_bgr, (uint)(src1_bgr.Length), src1_w, src1_h,
                ref face);

            Rst r = NewRst(ref nrst, err_desc);

            return r;
        }


        //
        // attribute
        //

        public Rst CheckMask(byte[] src1_bgr, int src1_w, int src1_h, ref Face face)
        {
            Native.ErrDesc err_desc = new Native.ErrDesc();

            Native.fsdkc_rst nrst = Native.fsdkc_attr_check_mask(
                err_desc.data_, err_desc.siz_,
                src1_bgr, (uint)(src1_bgr.Length), src1_w, src1_h,
                ref face);

            Rst r = NewRst(ref nrst, err_desc);

            if (r.IsOk())
            {
                r.face_mask_confidence = nrst.face_mask_confidence;
            }

            return r;
        }

        // Determine if the eyes and nose of the face are covered.
        public Rst DetectFaceOcclusion(byte[] src1_bgr, int src1_w, int src1_h, ref Face face)
        {
            Native.ErrDesc err_desc = new Native.ErrDesc();

            Native.fsdkc_face_occlusion_rst nrst = Native.fsdkc_attr_detect_face_occlusion(
                err_desc.data_, err_desc.siz_,
                src1_bgr, (uint)src1_bgr.Length,
                src1_w, src1_h, ref face);

            Rst r = NewRst(ref nrst, err_desc);

            return r;
        }

        // Detect fine-grained occlusion of the face
        public Rst DetectFineOcclusion(byte[] src1_bgr, int src1_w, int src1_h, ref Face face)
        {
            Native.ErrDesc err_desc = new Native.ErrDesc();

            Native.fsdkc_confidence_rst nrst = Native.fsdkc_attr_detect_fine_occlusion(
                err_desc.data_, err_desc.siz_,
                src1_bgr, (uint)src1_bgr.Length,
                src1_w, src1_h, ref face);

            Rst r = NewRst(ref nrst, err_desc);

            r.face_fine_occlu_score = nrst.confidence;

            return r;
        }


        //
        // Crop
        //

        public struct FaceCropAreaRst
        {
            public bool is_valid;

            public int x;
            public int y;
            public int w;
            public int h;

            public float margin_ratio;

            public int resize_w; // 0 for no resize
            public int resize_h; // 0 for no resize
        }

        public static FaceCropAreaRst CalcFaceCropArea(byte[] src_bgr, int src_w, int src_h, Box face_box,
            float crop_face_w_h_margin_ratio = 1.0f, int resize_width_after_crop = 0)
        {
            Native.fsdkc_crop_face_area_in_img_rst nrst
                = Native.fsdkc_calc_crop_face_area_in_img(
                    src_w, src_h,
                    face_box.x, face_box.y, face_box.w, face_box.h,
                    crop_face_w_h_margin_ratio,
                    resize_width_after_crop);

            return new FaceCropAreaRst
            {
                is_valid = nrst.last_err == 0 ? true : false,
                x = nrst.x, y = nrst.y, w = nrst.w, h = nrst.h,
                margin_ratio = nrst.margin_ratio,
                resize_w = nrst.resize_w, resize_h = nrst.resize_h
            };
        }


        public static BlobImg CropFaceFromImg(byte[] src_bgr, int src_w, int src_h,
            FaceCropAreaRst crop_area)
        {
            if (!crop_area.is_valid)
                return null;

            Native.ErrDesc err_desc = new Native.ErrDesc();

            Native.fsdkc_crop_face_rst nrst
                = Native.fsdkc_crop_face_from_img(
                    err_desc.data_, err_desc.siz_,
                    src_bgr, (uint)src_bgr.Length, src_w, src_h,
                    crop_area.x, crop_area.y, crop_area.w, crop_area.h,
                    crop_area.resize_w, crop_area.resize_h);

            if (nrst.last_err != 0)
                return null;

            // copy native allocate memory -> c#
            IntPtr nat_buf_ptr = nrst._face_bgr_buf__need_nat_free;
            uint nat_buf_siz = nrst.face_bgr_buf_siz;

            if (nat_buf_ptr == null || nat_buf_ptr == IntPtr.Zero)
                return null;

            byte[] bgr_img = new byte[nat_buf_siz];
            Marshal.Copy(nat_buf_ptr, bgr_img, 0, (int)nat_buf_siz);

            // free native allocated
            Native.fsdkc_free(nat_buf_ptr);

            return new BlobImg(bgr_img, nrst.face_w, nrst.face_h);
        }

        public static float ClampNearZero1e6f(float v) // 1e-6f, softmax, sigmoid
        {
            const float EPS = 1e-6f;
            return Math.Abs(v) < EPS ? 0f : v;
        }

    } // class FaceSDK


    //
    // Raw Image support via OpenCV
    //

    //
    // In face detection/recognition pipelines, Bitmaps decoded via Windows GDI+ may yield inconsistent results
    // depending on the OS version and internal GDI+ decoders. This can lead to unreliable pixel data.
    // It is recommended to standardize image decoding using OpenCV (ImDecode).
    //
    // DO-NOT-LOAD-IMAGE Via "new Bitmap(File.ReadAll('a.png'))" , USE OpenCV.ImDecode()
    //

    public class RawImageCV
    {
        //
        // Native (FaceSDKCS.dll)
        //
        private class Native
        {
            public const int FSDKC_RST_ERR_DESC_MAX = 256;

            public class ErrDesc
            {
                public ErrDesc()
                {
                    siz_ = FSDKC_RST_ERR_DESC_MAX;
                    data_ = new byte[siz_];
                }

                public string GetErrStr()
                {
                    return Encoding.ASCII.GetString(data_).TrimEnd('\0');
                }

                public byte[] data_;
                public uint siz_;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
            public struct fsdkc_raw_img_cv_bool_rst
            {
                public int last_err;
                public int false0_true1_rst;  // 0: false,  1: true
            };

            [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
            public struct fsdkc_raw_img_cv_make_rst
            {
                public int last_err;

                public int img_w;
                public int img_h;
                public int img_bytes_pp;         // bytes per pixel, opencv_img.elem_siz;   

                public int img_hint_channels;    // hint, channel count, bytes per channel: img_bpp / img_hint_channels

                public uint img_flag;       // 0x1: float image

                public uint img_buf_siz;   // fsdk_siz_t is always uint

                // must be freed by fsdkc_free() from caller(c#, ..)
                public IntPtr _img_buf__need_nat_free;
            };

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_raw_img_cv_make_rst fsdkc_make_raw_img_cv_from_img_file_path(
                    [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                    String mdl_path);

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_raw_img_cv_make_rst fsdkc_make_raw_img_cv_from_img_file_bytes(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] img_file_bytes, uint img_file_bytes_siz);

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern fsdkc_raw_img_cv_bool_rst fsdkc_save_raw_img_cv_to_file(
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] err_desc, uint err_desc_siz,
                [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] img_raw_buf, uint img_raw_buf_siz, int img_w, int img_h,
                int bytes_per_pixel /* 3:BGR-CV_8UC3*/,
                String img_cv_mat_fmt /*CV_8UC3*/,
                String img_file_path,
                String img_file_fmt  /*bmp, jpg, png, tif, raw*/,
                int img_file_prm0    /* -1:use default, jpg: jpg_quality:90,  png: png_compression=3 */,
                String img_file_prm1 /*null*/
            );

            [DllImport("AlcheraFaceSDKCS.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern void fsdkc_free(IntPtr m);
        }

        public struct RawImgBoolRst
        {
            public bool IsOk() => last_err == Error.NoError;
            public bool IsErr() => last_err != Error.NoError;
            public Error GetLastErr() => last_err;
            public string GetLastErrDesc() => last_err_desc ?? "";

            public Error last_err;
            public string last_err_desc;

            public bool result;
        }

        private static RawImgBoolRst NewBoolRst(Error err, string err_desc)
        {
            RawImgBoolRst r = new RawImgBoolRst();

            r.last_err = err;
            r.last_err_desc = err_desc ?? "";

            return r;
        }

        private static RawImgBoolRst NewBoolRst(ref Native.fsdkc_raw_img_cv_bool_rst nrst, Native.ErrDesc err_desc)
        {
            RawImgBoolRst r = new RawImgBoolRst();

            r.last_err = (Error)nrst.last_err;
            r.last_err_desc = err_desc.GetErrStr();

            r.result = nrst.false0_true1_rst == 1 ? true : false;

            return r;
        }


        public struct RawImgRst
        {
            public bool IsOk() => last_err == Error.NoError;
            public bool IsErr() => last_err != Error.NoError;
            public Error GetLastErr() => last_err;
            public string GetLastErrDesc() => last_err_desc ?? "";
 
            public Error last_err;
            public string last_err_desc;

            public int w;
            public int h;
            public int bytes_pp;  // bytes_per_pixel, => 3: RGB, 1: G

            // bytes per pixel-channel : bpp / pixel_hint_channels
            public int pixel_hint_channels; 

            public uint flag;           // 0x1: float image (F16)

            public int pixel_buf_siz;   // in c#, [< 2GB]: int,  [>2GB] IntPtr ptr = Marshal.AllocHGlobal(largeSize);
            public byte[] pixel_buf;
        }


        public struct Rst<T>
        {
            public bool IsOk() => last_err == Error.NoError;
            public bool IsErr() => last_err != Error.NoError;

            public Error GetLastErr() => last_err;
            public string GetLastErrDesc() => last_err_desc ?? string.Empty;

            public Error last_err;
            public string last_err_desc;

            public T value;
        }
        
        private static RawImgRst NewRst(Error err, string err_desc)
        {
            RawImgRst r = new RawImgRst();

            r.last_err = err;
            r.last_err_desc = err_desc ?? "";

            return r;
        }        

        private static RawImgRst NewRst(ref Native.fsdkc_raw_img_cv_make_rst nrst, Native.ErrDesc err_desc)
        {
            RawImgRst r = new RawImgRst();

            r.last_err = (Error)nrst.last_err;
            r.last_err_desc = err_desc.GetErrStr();

            if (r.IsOk())
            {
                if (nrst.img_buf_siz > (uint)Int32.MaxValue)
                {
                    Native.fsdkc_free(nrst._img_buf__need_nat_free);
                    nrst._img_buf__need_nat_free = IntPtr.Zero;

                    r.last_err = Error.SystemFunctionError;
                    r.last_err_desc = "can't load too large image, siz=" + nrst.img_buf_siz.ToString();
                }
                else
                {
                    int img_buf_siz = (int)nrst.img_buf_siz;

                    r.w = nrst.img_w;
                    r.h = nrst.img_h;
                    r.bytes_pp = nrst.img_bytes_pp;

                    r.pixel_hint_channels = nrst.img_hint_channels;
                    r.flag = nrst.img_flag;

                    r.pixel_buf_siz = img_buf_siz;
                    r.pixel_buf = new byte[r.pixel_buf_siz];
                    Marshal.Copy(nrst._img_buf__need_nat_free, r.pixel_buf, 0, r.pixel_buf_siz);

                    Native.fsdkc_free(nrst._img_buf__need_nat_free);
                    nrst._img_buf__need_nat_free = IntPtr.Zero;
                }
            }

#if DEBUG
            if (nrst._img_buf__need_nat_free != IntPtr.Zero)
            {
                throw new InvalidOperationException("invalid native operation, _img_buf__need_nat_free must be null");
            }
#endif

            return r;
        }

        public static RawImgRst MakeRawImgFromImgFile(string file_path)
        {
            if (file_path == null || file_path == "")
                return NewRst(Error.NothingAtInput, "invalid param file_path, null or empty");

            Native.ErrDesc err_desc = new Native.ErrDesc();

            Native.fsdkc_raw_img_cv_make_rst nrst
                = Native.fsdkc_make_raw_img_cv_from_img_file_path(
                    err_desc.data_, err_desc.siz_, file_path);

            RawImgRst r = NewRst(ref nrst, err_desc);

            return r;
        }

        public static RawImgRst MakeRawImgFromImgFileBytes(byte[] img_file_bytes)
        {
            if(img_file_bytes == null || img_file_bytes.Length == 0)
                return NewRst(Error.NothingAtInput, "invalid param img_file_bytes, null or length=0");

            Native.ErrDesc err_desc = new Native.ErrDesc();

            Native.fsdkc_raw_img_cv_make_rst nrst
                = Native.fsdkc_make_raw_img_cv_from_img_file_bytes(
                    err_desc.data_, err_desc.siz_, img_file_bytes, (uint)img_file_bytes.Length);

            RawImgRst r = NewRst(ref nrst, err_desc);

            return r;
        }

        public static RawImgBoolRst SaveBGRAsImgFile(string file_path,
            byte[] bgr_pixels /* must be BGR, bytes_per_pixel=3 byte */,
            int w, int h,
            string img_file_fmt = "jpg"   /* jpg,png,bmp,tif,raw */,
            int img_file_prm0 = -1,        /* -1: use default, 95:jpg, 3:png */
            string img_file_prm1 = "")
        {
            if (file_path == null || file_path == "")
                return NewBoolRst(Error.NothingAtInput, "invalid param file_path");

            int img_bytes_per_pixel = 3; // BGR, B8G8R8
            string img_cv_mat_fmt = "CV_8UC3";

            Native.ErrDesc err_desc = new Native.ErrDesc();

            Native.fsdkc_raw_img_cv_bool_rst nrst
                = Native.fsdkc_save_raw_img_cv_to_file(
                    err_desc.data_, err_desc.siz_,
                    bgr_pixels, (uint)bgr_pixels.Length, w, h,
                    img_bytes_per_pixel, img_cv_mat_fmt,
                    file_path, img_file_fmt, img_file_prm0, img_file_prm1);

            RawImgBoolRst r = NewBoolRst(ref nrst, err_desc);

            return r;
        }

        public static Rst<Bitmap> MakeBgrBitmapFromImgFileBytes(byte[] img_file_bytes)
        {
            Rst<Bitmap> rst = new Rst<Bitmap>();

            if(img_file_bytes == null || img_file_bytes.Length == 0)
            {
                rst.last_err = Error.NothingAtInput;
                rst.last_err_desc = "invalid param, img_file_bytes, null or length is 0";

                return rst;
            }

            RawImgRst raw_img_rst = MakeRawImgFromImgFileBytes(img_file_bytes);

            if(raw_img_rst.IsErr())
            {
                rst.last_err = raw_img_rst.GetLastErr();
                rst.last_err_desc = raw_img_rst.GetLastErrDesc();

                return rst;
            }

            PixelFormat pixel_fmt = PixelFormat.Format24bppRgb;
            // raw_img_rst.pixel_hint_channels == 1, Gray, pixel_fmt.Format8bppIndexed;

            if (raw_img_rst.pixel_hint_channels != 3 || raw_img_rst.bytes_pp != 3)
            {
                rst.last_err = Error.SystemFunctionError;
                rst.last_err_desc = $"img channel must be 3 and bytes_per_pixel must be 24, " +
                    $"chan={raw_img_rst.pixel_hint_channels}, bytes_pp={raw_img_rst.bytes_pp}";

                return rst;
            }

            int w = raw_img_rst.w;
            int h = raw_img_rst.h;

            Bitmap bmp = new Bitmap(w, h, pixel_fmt);

            var rect = new Rectangle(0, 0, w, h);
            BitmapData bmp_data = bmp.LockBits(rect, ImageLockMode.WriteOnly, pixel_fmt);

            try
            {
                int src_stride = w * raw_img_rst.bytes_pp;
                int dst_stride = bmp_data.Stride;

                IntPtr dst = bmp_data.Scan0;
                int src_offset = 0;

                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(raw_img_rst.pixel_buf, src_offset, dst, src_stride);
                    src_offset += src_stride;
                    dst = IntPtr.Add(dst, dst_stride);
                }
            }
            finally
            {
                bmp.UnlockBits(bmp_data);
            }

            rst.value = bmp;

            return rst;
        }

        public static Rst<BlobImg> MakeBgrFromImgFile(string file_path)
        {
            Rst<BlobImg> rst = new Rst<BlobImg>();

            if (file_path == null)
            {
                rst.last_err = Error.NothingAtInput;
                rst.last_err_desc = "invalid param, file_path, null or empty";
                return rst;
            }

            RawImgRst raw_img_rst = MakeRawImgFromImgFile(file_path);

            if(raw_img_rst.IsErr())
            {
                rst.last_err = raw_img_rst.GetLastErr();
                rst.last_err_desc = raw_img_rst.GetLastErrDesc();

                return rst;
            }

            if (raw_img_rst.pixel_hint_channels != 3 || raw_img_rst.bytes_pp != 3)
            {
                rst.last_err = Error.SystemFunctionError;
                rst.last_err_desc = $"img channel must be 3 and bytes_per_pixel must be 24, " +
                    $"chan={raw_img_rst.pixel_hint_channels}, bytes_pp={raw_img_rst.bytes_pp}";

                return rst;
            }

            rst.value = new BlobImg(raw_img_rst.pixel_buf, raw_img_rst.w, raw_img_rst.h);

            return rst;
        }

    } // end of RawImageCV


    //
    // Type
    //

    public class APP_TYPE
    {
        public const string SERVER = "SVR=ALIGN,LIVNESS,COMPARE;";
        public const string CLIENTSERVER = "SVR=LIVENESS,COMPARE;APP=ALIGN,BESTSHOT;";
        public const string CLIENTONLY = "SVR=COMPARE;APP=ALIGN,BESTSHOT,LIVENESS;";
        public const string SERVERLESS = "APP=ALIGN,BESTSHOT,LIVENESS,COMPARE;";
    }


} // namespace Alchera.FaceSDK
