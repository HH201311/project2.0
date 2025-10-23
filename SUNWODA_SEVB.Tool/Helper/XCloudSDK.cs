using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper
{
    public class XCloudSDK
    {
        public delegate int PXSDK_MessageCallBack(int hObject, int nMsgId, int nParam1, int nParam2, int nParam3, string szString, IntPtr pObject, int lParam, int nSeq, IntPtr pUserData, IntPtr pMsg);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Init(string sJsonParam);

        [DllImport("XCloudSDK.dll")]
        public static extern void XCloudSDK_UnInit();

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_RegisterCallback(PXSDK_MessageCallBack pMsgCallback, IntPtr pUserData);

        [DllImport("XCloudSDK.dll")]
        public static extern void XCloudSDK_UnRegister();

        [DllImport("XCloudSDK.dll")]
        public static extern int XSDK_SetSDKIntAttr(int nAttrKey, int nValue);

        [DllImport("XCloudSDK.dll")]
        public static extern int XSDK_GetSDKIntAttr(int nAttrKey, int nDefaultValue);

        [DllImport("XCloudSDK.dll")]
        public static extern int XSDK_SetSDKStrAttr(int nAttrKey, string sValue);

        [DllImport("XCloudSDK.dll")]
        public static extern string XSDK_GetSDKStrAttr(int nAttrKey, IntPtr pResult, int nMaxSize);

        [DllImport("XCloudSDK.dll")]
        public static extern int XSDK_SetObjIntAttr(int hObject, int nAttrKey, int nValue);

        [DllImport("XCloudSDK.dll")]
        public static extern int XSDK_GetObjIntAttr(int hObject, int nAttrKey, int nDefaultValue);

        [DllImport("XCloudSDK.dll")]
        public static extern int XSDK_SetObjStrAttr(int hObject, int nAttrKey, string sValue);

        [DllImport("XCloudSDK.dll")]
        public static extern string XSDK_GetObjStrAttr(int hObject, int nAttrKey, IntPtr pResult, int nMaxSize);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_SetLocalUserNameAndPwd(string sDevId, string sUser, string sPwd);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_DeleteDevsInfo(string sDevId);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_DevLogin(int hUser, string sDevId, int nSeq);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_DevLogout(string sDevId);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_DevGetSysConfig(int hUser, string szDevId, string szCommandName, int nCommand, int nTimeout, int nSeq);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_DevSetSysConfig(int hUser, string szDevId, string szCommandName, string pConfig, int nCommand, int nTimeout, int nSeq);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_DevGetChnConfig(int hUser, string szDevId, int nChannel, string szCommandName, int nCommand, int nTimeout, int nSeq);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_DevSetChnConfig(int hUser, string szDevId, int nChannel, string szCommandName, string pConfig, int nCommand, int nTimeout, int nSeq);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_SearchDevices(int hUser, int nTimeout, int nSeq);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_MediaRealPlay(int hUser, string sDevId, int nChannel, int nStreamType, IntPtr hWnd, int nSeq, string szParam);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_FindRecordFile(int hUser, string sDevId, string sParam, int nSeq);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_FindRecordFileByTime(int hUser, string sDevId, string sParam, int nSeq);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_MediaRecordPlay(int hUser, string sDevId, string sParam, IntPtr hWnd, int nSeq);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_MediaRecordPlayByTime(int hUser, string sDevId, string sParam, IntPtr hWnd, int nSeq);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_MediaRecordDownload(int hUser, string sDevId, string sParam, int nSeq);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_MediaRecordDownloadByTime(int hUser, string sDevId, string sParam, int nSeq);

        [DllImport("XCloudSDK.dll")]
        public static extern void XCloudSDK_Device_StopMediaPlay(int nPlayeHandle);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_MediaPause(int nPlayHandle, bool bPause);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_DevSynTime(int hUser, string szDevId, string szSysTime, int nSeq);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_DevSnap(int hUser, string szDevId, int nChannel, string sParam, int nSeq);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_StartAIListen(int hUser, string szDevId, string szReqJson, int nTimeout, int nSeq);

        [DllImport("XCloudSDK.dll")]
        public static extern void XCloudSDK_Device_StopAIListen(int nListenHandle);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Device_MakeKeyFrame(string szDevId, int nChannel, int nStreamType);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Play_MediaSnapImage(int nPlayHandle, string sFileName);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Play_StartRecord(int nPlayHandle, string szFileName);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Play_StopRecord(int nPlayHandle);

        [DllImport("XCloudSDK.dll")]
        public static extern int XCloudSDK_Play_PlaySound(int nPlayHandle, int nVolume, int nSeq);

    }
}
