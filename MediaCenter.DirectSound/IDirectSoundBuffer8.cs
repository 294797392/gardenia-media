﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace DirectSoundLib
{
    [ComImport]
    [Guid(IID.IID_IDirectSoundBuffer8)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDirectSoundBuffer8
    {
        [PreserveSig]
        int GetCaps(out DSCBCAPS pDSBufferCaps);

        [PreserveSig]
        int GetCurrentPosition(out uint pdwCurrentPlayCursor, out uint pdwCurrentWriteCursor);

        [PreserveSig]
        int GetFormat(out WAVEFORMATEX pwfxFormat, uint dwSizeAllocated, out uint pdwSizeWritten);

        [PreserveSig]
        int GetVolume(out int plVolume);

        [PreserveSig]
        int GetPan(out int plPan);

        [PreserveSig]
        int GetFrequency(out uint pdwFrequency);

        [PreserveSig]
        int GetStatus(out int pdwStatus);

        [PreserveSig]
        int Initialize(IntPtr pDirectSound, ref DSBUFFERDESC pcDSBufferDesc);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dwOffset">Offset, in bytes, from the start of the buffer to the point where the lock begins. This parameter is ignored if DSBLOCK_FROMWRITECURSOR is specified in the dwFlags parameter. </param>
        /// <param name="dwBytes"></param>
        /// <param name="ppvAudioPtr1"></param>
        /// <param name="pdwAudioBytes1"></param>
        /// <param name="ppvAudioPtr2"></param>
        /// <param name="pdwAudioBytes2"></param>
        /// <param name="dwFlags">
        /// DSBLOCK_FROMWRITECURSOR : Start the lock at the write cursor. The dwOffset parameter is ignored.
        /// DSBLOCK_ENTIREBUFFER : Lock the entire buffer. The dwBytes parameter is ignored.
        /// </param>
        /// <returns></returns>
        [PreserveSig]
        int Lock(int dwOffset, int dwBytes, out IntPtr ppvAudioPtr1, out int pdwAudioBytes1, out IntPtr ppvAudioPtr2, out int pdwAudioBytes2, int dwFlags);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dwReserved1">必须为0</param>
        /// <param name="dwPriority">声音优先级, 当分配硬件混合资源的时候用来管理声音, 最低级别为0, 最高级别0xFFFFFFFF, 如果缓冲区创建的时候没有设置DSBCAPS_LOCDEFER标志, 那么取值必须为0</param>
        /// <param name="dwFlags"></param>
        /// <returns></returns>
        [PreserveSig]
        int Play(uint dwReserved1, uint dwPriority, uint dwFlags);

        [PreserveSig]
        int SetCurrentPosition(uint dwNewPosition);

        [PreserveSig]
        int SetFormat(ref WAVEFORMATEX pcfxFormat);

        [PreserveSig]
        int SetVolume(int lVolume);

        [PreserveSig]
        int SetPan(int lPan);

        [PreserveSig]
        int SetFrequency(uint dwFrequency);

        [PreserveSig]
        int Stop();

        [PreserveSig]
        int Unlock(IntPtr pvAudioPtr1, int dwAudioBytes1, IntPtr pvAudioPtr2, int dwAudioBytes2);

        [PreserveSig]
        int Restore();

        [PreserveSig]
        int SetFX(uint dwEffectsCount, DSEFFECTDESC pDSFXDesc, out uint pdwResultCodes);

        [PreserveSig]
        int AcquireResources(uint dwFlags, uint dwEffectsCount, out uint pdwResultCodes);

        [PreserveSig]
        int GetObjectInPath(GUID rguidObject, int dwIndex, GUID rguidInterface, out IntPtr ppObject);
    }
}