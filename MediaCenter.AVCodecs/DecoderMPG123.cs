﻿using MediaCenter.Base;
using MediaCenter.Base.AVCodecs;
using MediaCenter.Base.AVDemuxers;
using System;
using System.Runtime.InteropServices;

namespace MediaCenter.AVCodecs
{
    public class DecoderMPG123 : AbstractAVDecoder
    {
        #region 类变量

        private static log4net.ILog logger = log4net.LogManager.GetLogger("DecoderMPG123");

        public const int MAX_PROBE_TIMES = 10;

        #endregion

        #region 实例变量

        private IntPtr mpg123Handle;
        private AbstractAVDemuxer demuxer;

        #endregion

        #region 属性

        public override string Name => "mpg123 audio decoder";

        #endregion

        static DecoderMPG123()
        {
            mpg123.mpg123_init();
        }

        #region 公开接口

        public override bool Open(AbstractAVDemuxer demuxer)
        {
            #region 初始化mpg123句柄

            int error;
            if ((this.mpg123Handle = mpg123.mpg123_new(IntPtr.Zero, out error)) == IntPtr.Zero)
            {
                logger.ErrorFormat("mpg123_new失败, {0}", error);
                return false;
            }

            if ((error = mpg123.mpg123_open_feed(this.mpg123Handle)) != mpg123.MPG123_OK)
            {
                logger.ErrorFormat("mpg123_open_feed失败, {0}", error);
                return false;
            }

            #endregion

            #region 探测流格式

            IntPtr container_hdr_ptr = Marshal.UnsafeAddrOfPinnedArrayElement(demuxer.ContainerHeader, 0);
            if ((error = mpg123.mpg123_feed(this.mpg123Handle, container_hdr_ptr, demuxer.ContainerHeader.Length)) != mpg123.MPG123_OK)
            {
                logger.ErrorFormat("mpg123_feed ContainerHeader失败, {0}", error);
                return false;
            }

            /* 如果当前没有解析数据帧，则读取一帧数据 */
            AudioDemuxPacket packet = null;
            if (demuxer.CurrentPacket == null)
            {
                if (!demuxer.DemuxNextPacket<AudioDemuxPacket>(out packet))
                {
                    logger.ErrorFormat("NextPacket失败");
                    return false;
                }
            }

            int total_probe_times = 0;
            int rate, channel, encoding;
            do
            {
                IntPtr frameptr = Marshal.UnsafeAddrOfPinnedArrayElement(packet.Data, 0);
                if ((error = mpg123.mpg123_feed(this.mpg123Handle, frameptr, packet.Data.Length)) != mpg123.MPG123_OK)
                {
                    logger.ErrorFormat("feed数据失败, {0}", error);
                    return false;
                }

                if (total_probe_times++ > MAX_PROBE_TIMES)
                {
                    logger.ErrorFormat("Open超时");
                    return false;
                }
            }
            while ((error = mpg123.mpg123_getformat(this.mpg123Handle, out rate, out channel, out encoding)) != mpg123.MPG123_OK);

            logger.InfoFormat("OpenDemuxer成功, rate={0}, channel={1}, encoding={2}", rate, channel, encoding);

            #endregion

            this.demuxer = demuxer;

            return true;
        }

        public override bool Close()
        {
            throw new NotImplementedException();
        }

        public override bool IsSupported(AVFormats formats)
        {
            switch (formats)
            {
                case AVFormats.MP3:
                case AVFormats.AAC:
                    return true;
                default:
                    return false;
            }
        }

        #endregion

        protected override bool DecodeDataCore(int target_size, out byte[] out_data, out int out_size)
        {
            out_size = 0;
            out_data = new byte[target_size + 65535];
            int total_decode = 0;
            int remain = target_size;
            while (true)
            {
                int done = 0;
                byte[] tmpbuf = new byte[remain];
                IntPtr tmpptr = Marshal.UnsafeAddrOfPinnedArrayElement(tmpbuf, 0);
                int ret = mpg123.mpg123_read(this.mpg123Handle, tmpptr, tmpbuf.Length, out done);
                if (done == 0 && ret == mpg123.MPG123_ERR)
                {
                    logger.ErrorFormat("mpg123_read失败, {0}", ret);
                    return false;
                }

                Buffer.BlockCopy(tmpbuf, 0, out_data, total_decode, done);
                total_decode += done;
                remain -= done;

                /* 解码的数据不够，继续解析音频帧然后解码 */
                if (total_decode < target_size)
                {
                    AudioDemuxPacket packet = null;
                    if (!this.demuxer.DemuxNextPacket<AudioDemuxPacket>(out packet))
                    {
                        return false;
                    }

                    IntPtr frameptr = Marshal.UnsafeAddrOfPinnedArrayElement(packet.Data, 0);
                    if ((ret = mpg123.mpg123_feed(this.mpg123Handle, frameptr, packet.Data.Length)) != mpg123.MPG123_OK)
                    {
                        return false;
                    }
                }
                else
                {
                    return true;
                }
            }
        }

        private enum mpg123_enc_enum
        {
            /* 0000 0000 0000 1111 Some 8 bit  integer encoding. */
            MPG123_ENC_8 = 0x00f
    /* 0000 0000 0100 0000 Some 16 bit integer encoding. */
    , MPG123_ENC_16 = 0x040
    /* 0100 0000 0000 0000 Some 24 bit integer encoding. */
    , MPG123_ENC_24 = 0x4000
    /* 0000 0001 0000 0000 Some 32 bit integer encoding. */
    , MPG123_ENC_32 = 0x100
    /* 0000 0000 1000 0000 Some signed integer encoding. */
    , MPG123_ENC_SIGNED = 0x080
    /* 0000 1110 0000 0000 Some float encoding. */
    , MPG123_ENC_FLOAT = 0xe00
    /* 0000 0000 1101 0000 signed 16 bit */
    , MPG123_ENC_SIGNED_16 = (MPG123_ENC_16 | MPG123_ENC_SIGNED | 0x10)
    /* 0000 0000 0110 0000 unsigned 16 bit */
    , MPG123_ENC_UNSIGNED_16 = (MPG123_ENC_16 | 0x20)
    /* 0000 0000 0000 0001 unsigned 8 bit */
    , MPG123_ENC_UNSIGNED_8 = 0x01
    /* 0000 0000 1000 0010 signed 8 bit */
    , MPG123_ENC_SIGNED_8 = (MPG123_ENC_SIGNED | 0x02)
    /* 0000 0000 0000 0100 ulaw 8 bit */
    , MPG123_ENC_ULAW_8 = 0x04
    /* 0000 0000 0000 1000 alaw 8 bit */
    , MPG123_ENC_ALAW_8 = 0x08
    /* 0001 0001 1000 0000 signed 32 bit */
    , MPG123_ENC_SIGNED_32 = MPG123_ENC_32 | MPG123_ENC_SIGNED | 0x1000
    /* 0010 0001 0000 0000 unsigned 32 bit */
    , MPG123_ENC_UNSIGNED_32 = MPG123_ENC_32 | 0x2000
    /* 0101 0000 1000 0000 signed 24 bit */
    , MPG123_ENC_SIGNED_24 = MPG123_ENC_24 | MPG123_ENC_SIGNED | 0x1000
    /* 0110 0000 0000 0000 unsigned 24 bit */
    , MPG123_ENC_UNSIGNED_24 = MPG123_ENC_24 | 0x2000
    /* 0000 0010 0000 0000 32bit float */
    , MPG123_ENC_FLOAT_32 = 0x200
    /* 0000 0100 0000 0000 64bit float */
    , MPG123_ENC_FLOAT_64 = 0x400
    /* Any possibly known encoding from the list above. */
    , MPG123_ENC_ANY = (MPG123_ENC_SIGNED_16 | MPG123_ENC_UNSIGNED_16
               | MPG123_ENC_UNSIGNED_8 | MPG123_ENC_SIGNED_8
               | MPG123_ENC_ULAW_8 | MPG123_ENC_ALAW_8
               | MPG123_ENC_SIGNED_32 | MPG123_ENC_UNSIGNED_32
               | MPG123_ENC_SIGNED_24 | MPG123_ENC_UNSIGNED_24
               | MPG123_ENC_FLOAT_32 | MPG123_ENC_FLOAT_64)

        }

        /// <summary>
        /// mpg123解码库封装
        /// </summary>
        private static class mpg123
        {
            public static readonly int MPG123_DONE = -12;   /**< Message: Track ended. Stop decoding. */

            // 意思是mpg123已经探测到了文件的格式，找到了新格式。。。
            public static readonly int MPG123_NEW_FORMAT = -11; /**< Message: Output format will be different on next call. Note that some libmpg123 versions between 1.4.3 and 1.8.0 insist on you calling mpg123_getformat() after getting this message code. Newer verisons behave like advertised: You have the chance to call mpg123_getformat(), but you can also just continue decoding and get your data. */
            public static readonly int MPG123_NEED_MORE = -10;  /**< Message: For feed reader: "Feed me more!" (call mpg123_feed() or mpg123_decode() with some new input data). */
            public static readonly int MPG123_ERR = -1;         /**< Generic Error */
            public static readonly int MPG123_OK = 0;           /**< Success */
            public static readonly int MPG123_BAD_OUTFORMAT = 1;    /**< Unable to set up output format! */
            public static readonly int MPG123_BAD_CHANNEL = 2;      /**< Invalid channel number specified. */
            public static readonly int MPG123_BAD_RATE = 3;     /**< Invalid sample rate specified.  */
            public static readonly int MPG123_ERR_16TO8TABLE = 4;   /**< Unable to allocate memory for 16 to 8 converter table! */
            public static readonly int MPG123_BAD_PARAM = 5;        /**< Bad parameter id! */
            public static readonly int MPG123_BAD_BUFFER = 6;/**< Bad buffer given -- invalid pointer or too small size. */
            public static readonly int MPG123_OUT_OF_MEM = 7;       /**< Out of memory -- some malloc() failed. */
            public static readonly int MPG123_NOT_INITIALIZED = 8;  /**< You didn't initialize the library! */
            public static readonly int MPG123_BAD_DECODER = 9;      /**< Invalid decoder choice. */
            public static readonly int MPG123_BAD_HANDLE = 10;       /**< Invalid mpg123 handle. */
            public static readonly int MPG123_NO_BUFFERS = 11;       /**< Unable to initialize frame buffers (out of memory?). */
            public static readonly int MPG123_BAD_RVA = 12;          /**< Invalid RVA mode. */
            public static readonly int MPG123_NO_GAPLESS = 13;       /**< This build doesn't support gapless decoding. */
            public static readonly int MPG123_NO_SPACE = 14;     /**< Not enough buffer space. */
            public static readonly int MPG123_BAD_TYPES = 15;        /**< Incompatible numeric data types. */
            public static readonly int MPG123_BAD_BAND = 16;     /**< Bad equalizer band. */
            public static readonly int MPG123_ERR_NULL = 17;     /**< Null pointer given where valid storage address needed. */
            public static readonly int MPG123_ERR_READER = 18;       /**< Error reading the stream. */
            public static readonly int MPG123_NO_SEEK_FROM_END = 19;/**< Cannot seek from end (end is not known). */
            public static readonly int MPG123_BAD_WHENCE = 20;       /**< Invalid 'whence' for seek function.*/
            public static readonly int MPG123_NO_TIMEOUT = 21;       /**< Build does not support stream timeouts. */
            public static readonly int MPG123_BAD_FILE = 22;     /**< File access error. */
            public static readonly int MPG123_NO_SEEK = 23;          /**< Seek not supported by stream. */
            public static readonly int MPG123_NO_READER = 24;        /**< No stream opened. */
            public static readonly int MPG123_BAD_PARS = 25;     /**< Bad parameter handle. */
            public static readonly int MPG123_BAD_INDEX_PAR = 26;    /**< Bad parameters to mpg123_index() and mpg123_set_index() */
            public static readonly int MPG123_OUT_OF_SYNC = 27;  /**< Lost track in bytestream and did not try to resync. */
            public static readonly int MPG123_RESYNC_FAIL = 28;  /**< Resync failed to find valid MPEG data. */
            public static readonly int MPG123_NO_8BIT = 29;  /**< No 8bit encoding possible. */
            public static readonly int MPG123_BAD_ALIGN = 30;    /**< Stack aligmnent error */
            public static readonly int MPG123_NULL_BUFFER = 31;  /**< NULL input buffer with non-zero size... */
            public static readonly int MPG123_NO_RELSEEK = 32;   /**< Relative seek not possible (screwed up file offset) */
            public static readonly int MPG123_NULL_POINTER = 33; /**< You gave a null pointer somewhere where you shouldn't have. */
            public static readonly int MPG123_BAD_KEY = 34;  /**< Bad key value given. */
            public static readonly int MPG123_NO_INDEX = 35; /**< No frame index in this build. */
            public static readonly int MPG123_INDEX_FAIL = 36;   /**< Something with frame index went wrong. */
            public static readonly int MPG123_BAD_DECODER_SETUP = 37;    /**< Something prevents a proper decoder setup */
            public static readonly int MPG123_MISSING_FEATURE = 38; /**< This feature has not been built into libmpg123. */
            public static readonly int MPG123_BAD_VALUE = 39; /**< A bad value has been given, somewhere. */
            public static readonly int MPG123_LSEEK_FAILED = 40; /**< Low-level seek failed. */
            public static readonly int MPG123_BAD_CUSTOM_IO = 41; /**< Custom I/O not prepared. */
            public static readonly int MPG123_LFS_OVERFLOW = 42; /**< Offset value overflow during translation of large file API calls -- your client program cannot handle that large file. */
            public static readonly int MPG123_INT_OVERFLOW = 43; /**< Some integer overflow. */





            private const string DLLNAME_LIBMPG123 = "libmpg123-0.dll";

            /// <summary>
            /// Function to initialise the mpg123 library. This function is not thread-safe. Call it exactly once per process, before any other (possibly threaded) work with the library.
            /// </summary>
            /// <returns>MPG123_OK if successful, otherwise an error number.</returns>
            [DllImport(DLLNAME_LIBMPG123, CallingConvention = CallingConvention.Cdecl)]
            public static extern int mpg123_init();

            /// <summary>
            /// Function to close down the mpg123 library. This function is not thread-safe. Call it exactly once per process, before any other (possibly threaded) work with the library.
            /// </summary>
            /// <returns></returns>
            [DllImport(DLLNAME_LIBMPG123, CallingConvention = CallingConvention.Cdecl)]
            public static extern void mpg123_exit();

            /// <summary>
            /// Create a handle with optional choice of decoder (named by a string, see mpg123_decoders() or mpg123_supported_decoders()). and optional retrieval of an error code to feed to mpg123_plain_strerror(). Optional means: Any of or both the parameters may be NULL.
            /// </summary>
            /// <param name="decoder">choice of decoder variant (NULL for default)</param>
            /// <param name="error">address to store error codes</param>
            /// <returns>Non-NULL pointer to fresh handle when successful.</returns>
            [DllImport(DLLNAME_LIBMPG123, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr mpg123_new(IntPtr decoder, out int error);

            /// <summary>
            /// Delete handle, mh is either a valid mpg123 handle or NULL.
            /// </summary>
            /// <param name="mh">mpg123_new创建的handle</param>
            [DllImport(DLLNAME_LIBMPG123, CallingConvention = CallingConvention.Cdecl)]
            public static extern void mpg123_delete(IntPtr mh);

            /// <summary>
            /// Set a specific parameter, for a specific mpg123_handle, using a parameter type key chosen from the mpg123_parms enumeration, to the specified value.
            /// </summary>
            /// <param name="mh"></param>
            /// <param name="type">parameter choice</param>
            /// <param name="value">integer value</param>
            /// <param name="fValue">floating point value</param>
            /// <returns>MPG123_OK on success</returns>
            [DllImport(DLLNAME_LIBMPG123, CallingConvention = CallingConvention.Cdecl)]
            public static extern int mpg123_param(IntPtr mh, int type, int value, IntPtr fValue);

            /// <summary>
            /// Get a specific parameter, for a specific mpg123_handle. See the mpg123_parms enumeration for a list of available parameters.
            /// </summary>
            /// <param name="mh"></param>
            /// <param name="type">parameter choice</param>
            /// <param name="value">integer value</param>
            /// <param name="fValue">floating point value</param>
            /// <returns>MPG123_OK on success</returns>
            [DllImport(DLLNAME_LIBMPG123, CallingConvention = CallingConvention.Cdecl)]
            public static extern int mpg123_getparam(IntPtr mh, int type, out int value, out IntPtr fValue);

            /// <summary>
            /// Decode MPEG Audio from inmemory to outmemory. This is very close to a drop-in replacement for old mpglib. When you give zero-sized output buffer the input will be parsed until decoded data is available. This enables you to get MPG123_NEW_FORMAT (and query it) without taking decoded data. Think of this function being the union of mpg123_read() and mpg123_feed() (which it actually is, sort of;-). You can actually always decide if you want those specialized functions in separate steps or one call this one here.
            /// </summary>
            /// <param name="mh"></param>
            /// <param name="inmemory"></param>
            /// <param name="inmemsize"></param>
            /// <param name="outmemory"></param>
            /// <param name="outmemsize">maximum number of output bytes</param>
            /// <param name="done">address to store the number of actually decoded bytes to</param>
            /// <returns></returns>
            [DllImport(DLLNAME_LIBMPG123, CallingConvention = CallingConvention.Cdecl)]
            public static extern int mpg123_decode(IntPtr mh, IntPtr inmemory, int inmemsize, IntPtr outmemory, int outmemsize, out int done);

            /// <summary>
            /// Open a new bitstream and prepare for direct feeding This works together with mpg123_decode(); you are responsible for reading and feeding the input bitstream.
            /// </summary>
            /// <param name="mh"></param>
            /// <returns>MPG123_OK on success</returns>
            [DllImport(DLLNAME_LIBMPG123, CallingConvention = CallingConvention.Cdecl)]
            public static extern int mpg123_open_feed(IntPtr mh);

            [DllImport(DLLNAME_LIBMPG123, CallingConvention = CallingConvention.Cdecl)]
            public static extern int mpg123_getformat(IntPtr mh, out int rate, out int channels, out int encoding);

            /// <summary>
            /// Feed data for a stream that has been opened with mpg123_open_feed(). It's give and take: You provide the bytestream, mpg123 gives you the decoded samples.
            /// </summary>
            /// <param name="mh"></param>
            /// <param name="data"></param>
            /// <param name="size"></param>
            /// <returns>MPG123_OK or error/message code.</returns>
            [DllImport(DLLNAME_LIBMPG123, CallingConvention = CallingConvention.Cdecl)]
            public static extern int mpg123_feed(IntPtr mh, IntPtr data, int size);

            /// <summary>
            /// 获取音频流的采样长度（采样数）
            /// </summary>
            /// <param name="mg"></param>
            /// <returns></returns>
            [DllImport(DLLNAME_LIBMPG123, CallingConvention = CallingConvention.Cdecl)]
            public static extern int mpg123_length(IntPtr mg);

            [DllImport(DLLNAME_LIBMPG123, CallingConvention = CallingConvention.Cdecl)]
            public static extern int mpg123_read(IntPtr mg, IntPtr outmemory, int outmemsize, out int done);
        }
    }
}