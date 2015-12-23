﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using vibrance.GUI.common;

namespace vibrance.GUI.NVIDIA
{
    class NvidiaDynamicVibranceProxy : IVibranceProxy
    {
        #region DllImports
        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?initializeLibrary@vibrance@vibranceDLL@@QAE_NXZ",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Auto)]
        static extern bool initializeLibrary();

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?unloadLibrary@vibrance@vibranceDLL@@QAE_NXZ",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Auto)]
        static extern bool unloadLibrary();


        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?getActiveOutputs@vibrance@vibranceDLL@@QAEHQAPAH0@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Auto)]
        static extern int getActiveOutputs([In, Out] int[] gpuHandles, [In, Out] int[] outputIds);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?enumeratePhsyicalGPUs@vibrance@vibranceDLL@@QAEXQAPAH@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Auto)]
        static extern void enumeratePhsyicalGPUs([In, Out] int[] gpuHandles);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?getGpuName@vibrance@vibranceDLL@@QAE_NQAPAHPAD@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        static extern bool getGpuName([In, Out] int[] gpuHandles, StringBuilder szName);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?getDVCInfo@vibrance@vibranceDLL@@QAE_NPAUNV_DISPLAY_DVC_INFO@12@H@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        static extern bool getDVCInfo(ref NV_DISPLAY_DVC_INFO info, int defaultHandle);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?enumerateNvidiaDisplayHandle@vibrance@vibranceDLL@@QAEHH@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Auto)]
        static extern int enumerateNvidiaDisplayHandle(int index);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?setDVCLevel@vibrance@vibranceDLL@@QAE_NHH@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Auto)]
        static extern bool setDVCLevel([In] int defaultHandle, [In] int level);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?isCsgoActive@vibrance@vibranceDLL@@QAE_NPAPAUHWND__@@@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Auto)]
        static extern bool isCsgoActive(ref IntPtr hwnd);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?isCsgoStarted@vibrance@vibranceDLL@@QAE_NPAPAUHWND__@@@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        static extern bool isCsgoStarted(ref IntPtr hwnd);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?equalsDVCLevel@vibrance@vibranceDLL@@QAE_NHH@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Auto)]
        static extern bool equalsDVCLevel([In] int defaultHandle, [In] int level);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        static extern int GetWindowTextLength([In] IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        static extern int GetWindowTextA([In] IntPtr hWnd, [In, Out] StringBuilder lpString, [In] int nMaxCount);

        [DllImport(
            "vibranceDLL.dll",
            EntryPoint = "?getAssociatedNvidiaDisplayHandle@vibrance@vibranceDLL@@QAEHPBDH@Z",
            CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        static extern int getAssociatedNvidiaDisplayHandle(string deviceName, [In] int length);
        #endregion


        public const int NVAPI_MAX_PHYSICAL_GPUS = 64;
        public const int NVAPI_MAX_LEVEL = 63;
        public const int NVAPI_DEFAULT_LEVEL = 0;

        public const string NVAPI_ERROR_INIT_FAILED = "VibranceProxy failed to initialize! Read readme.txt for fix!";

        private static VIBRANCE_INFO vibranceInfo;
        private static List<NvidiaApplicationSetting> applicationSettings;
        private static ResolutionModeWrapper windowsResolutionSettings;
        private WinEventHook hook;

        public NvidiaDynamicVibranceProxy(ref List<NvidiaApplicationSetting> savedApplicationSettings, ResolutionModeWrapper currentWindowsResolutionSettings)
        {
            try
            {
                applicationSettings = savedApplicationSettings;
                windowsResolutionSettings = currentWindowsResolutionSettings;
                vibranceInfo = new VIBRANCE_INFO();
                if (initializeLibrary())
                {
                    initializeProxy();
                }

                if (vibranceInfo.isInitialized)
                {
                    hook = WinEventHook.GetInstance();
                    hook.WinEventHookHandler += OnWinEventHook;
                }

            }
            catch (Exception)
            {
                MessageBox.Show(NvidiaVibranceProxy.NVAPI_ERROR_INIT_FAILED);
            }
        }

        private void initializeProxy()
        {
            int[] gpuHandles = new int[NVAPI_MAX_PHYSICAL_GPUS];
            int[] outputIds = new int[NVAPI_MAX_PHYSICAL_GPUS];
            enumeratePhsyicalGPUs(gpuHandles);

            enumerateDisplayHandles();

            vibranceInfo.activeOutput = getActiveOutputs(gpuHandles, outputIds);
            StringBuilder buffer = new StringBuilder(64);
            char[] sz = new char[64];
            getGpuName(gpuHandles, buffer);
            vibranceInfo.szGpuName = buffer.ToString();
            vibranceInfo.defaultHandle = enumerateNvidiaDisplayHandle(0);

            NV_DISPLAY_DVC_INFO info = new NV_DISPLAY_DVC_INFO();
            if (getDVCInfo(ref info, vibranceInfo.defaultHandle))
            {
                if (info.currentLevel != vibranceInfo.userVibranceSettingDefault)
                {
                    setDVCLevel(vibranceInfo.defaultHandle, vibranceInfo.userVibranceSettingDefault);
                }
            }

            vibranceInfo.isInitialized = true;
        }

        private static void OnWinEventHook(object sender, WinEventHookEventArgs e)
        {
            if (applicationSettings.Count > 0)
            {
                NvidiaApplicationSetting applicationSetting = applicationSettings.FirstOrDefault(x => x.Name.Equals(e.ProcessName));
                if (applicationSetting != null)
                {
                    //test if a resolution change is needed
                    Screen screen = Screen.FromHandle(e.Handle);
                    if (isResolutionChangeNeeded(screen, applicationSetting.ResolutionSettings))
                    {
                        performResolutionChange(screen, applicationSetting.ResolutionSettings);
                    }

                    //test if changing the vibrance value is needed
                    if (!equalsDVCLevel(vibranceInfo.defaultHandle, applicationSetting.IngameLevel))
                    {
                        int displayHandle = getApplicationDisplayHandle(e.Handle);
                        if (displayHandle != -1)
                        {
                            vibranceInfo.defaultHandle = displayHandle;
                        }
                        setDVCLevel(vibranceInfo.defaultHandle, applicationSetting.IngameLevel);
                    }
                }
                else
                {
                    IntPtr processHandle = e.Handle;
                    if (!isCsgoActive(ref processHandle))
                        return;

                    //test if a resolution change is needed
                    Screen screen = Screen.FromHandle(processHandle);
                    if (isResolutionChangeNeeded(screen, windowsResolutionSettings))
                    {
                        performResolutionChange(screen, windowsResolutionSettings);
                    }

                    //test if changing the vibrance value is needed
                    if (vibranceInfo.affectPrimaryMonitorOnly && !equalsDVCLevel(vibranceInfo.defaultHandle, vibranceInfo.userVibranceSettingDefault))
                    {
                        setDVCLevel(vibranceInfo.defaultHandle, vibranceInfo.userVibranceSettingDefault);
                    }
                    else if (!vibranceInfo.affectPrimaryMonitorOnly && !vibranceInfo.displayHandles.TrueForAll(handle => equalsDVCLevel(handle, vibranceInfo.userVibranceSettingDefault)))
                    {
                        vibranceInfo.displayHandles.ForEach(handle => setDVCLevel(handle, vibranceInfo.userVibranceSettingDefault));
                    }
                }
            }
        }

        private static bool isResolutionChangeNeeded(Screen screen, ResolutionModeWrapper resolutionSettings)
        {
            if (resolutionSettings != null && (screen.Bounds.Height != resolutionSettings.dmPelsHeight
                || screen.Bounds.Width != resolutionSettings.dmPelsWidth))
            {
                return true;
            }
            return false;
        }

        private static void performResolutionChange(Screen screen, ResolutionModeWrapper resolutionSettings)
        {
            ResolutionHelper.ChangeResolutionEx(resolutionSettings, screen.DeviceName);
        }

        private void enumerateDisplayHandles()
        {
            for (int i = 0, displayHandle = 0; displayHandle != -1; i++)
            {
                if (vibranceInfo.displayHandles == null)
                    vibranceInfo.displayHandles = new List<int>();

                displayHandle = enumerateNvidiaDisplayHandle(i);
                if (displayHandle != -1)
                    vibranceInfo.displayHandles.Add(displayHandle);
            }
        }

        private static int getApplicationDisplayHandle(IntPtr hWnd)
        {
            if (hWnd != IntPtr.Zero)
            {
                Screen primaryScreen = System.Windows.Forms.Screen.FromHandle(hWnd);
                if (primaryScreen != null)
                {
                    string deviceName = primaryScreen.DeviceName;
                    GCHandle handle = GCHandle.Alloc(deviceName, GCHandleType.Pinned);
                    int id = getAssociatedNvidiaDisplayHandle(deviceName, deviceName.Length);
                    handle.Free();

                    return id;
                }
            }
            return -1;
        }

        public void setApplicationSettings(ref List<NvidiaApplicationSetting> refApplicationSettings)
        {
            applicationSettings = refApplicationSettings;
        }

        public void setShouldRun(bool shouldRun)
        {
            vibranceInfo.shouldRun = shouldRun;
        }

        public void setVibranceWindowsLevel(int vibranceWindowsLevel)
        {
            vibranceInfo.userVibranceSettingDefault = vibranceWindowsLevel;
        }

        public void setVibranceIngameLevel(int vibranceIngameLevel)
        {
            vibranceInfo.userVibranceSettingActive = vibranceIngameLevel;
        }

        public void setKeepActive(bool keepActive)
        {
            vibranceInfo.keepActive = keepActive;
        }

        public void setSleepInterval(int interval)
        {
            vibranceInfo.sleepInterval = interval;
        }

        public void handleDVC()
        {
            //throw new NotImplementedException();
        }

        public void setAffectPrimaryMonitorOnly(bool affectPrimaryMonitorOnly)
        {
            vibranceInfo.affectPrimaryMonitorOnly = affectPrimaryMonitorOnly;
        }

        public VIBRANCE_INFO getVibranceInfo()
        {
            return vibranceInfo;
        }

        public bool unloadLibraryEx()
        {
            hook.removeWinEventHook();
            return unloadLibrary();
        }

        public void handleDVCExit()
        {
            if (vibranceInfo.affectPrimaryMonitorOnly)
            {
                setDVCLevel(vibranceInfo.defaultHandle, vibranceInfo.userVibranceSettingDefault);
            }
            else if (!vibranceInfo.displayHandles.TrueForAll(handle => equalsDVCLevel(handle, vibranceInfo.userVibranceSettingDefault)))
                vibranceInfo.displayHandles.ForEach(handle => setDVCLevel(handle, vibranceInfo.userVibranceSettingDefault));
        }
    }
}