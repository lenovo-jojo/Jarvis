using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using AICompanion.Core;
using Debug = UnityEngine.Debug;

namespace AICompanion.Services
{
    public class ContextService : MonoBehaviour, IContextService
    {
        private WebCamTexture webcamTexture;
        private Texture2D cameraFrameTex;
        private Texture2D desktopFrameTex;
        private float lastDesktopCaptureTime = -10f;
        private bool desktopCaptureWarningShown;
        private Process desktopCaptureProcess;
        private string desktopCapturePath;
        private float desktopCaptureStartedTime;
        private const float DesktopCaptureInterval = 0.75f;

        public void StartCamera()
        {
            if (webcamTexture == null)
            {
                WebCamDevice[] devices = WebCamTexture.devices;
                if (devices.Length > 0)
                {
                    webcamTexture = new WebCamTexture(devices[0].name, 640, 480, 30);
                    webcamTexture.Play();
                    Debug.Log($"Started webcam: {devices[0].name}");
                }
                else
                {
                    Debug.LogWarning("No camera device found.");
                }
            }
            else if (!webcamTexture.isPlaying)
            {
                webcamTexture.Play();
            }
        }

        public void StopCamera()
        {
            if (webcamTexture != null && webcamTexture.isPlaying)
            {
                webcamTexture.Stop();
                Debug.Log("Stopped webcam.");
            }
        }

        public Texture2D GetCameraFrame()
        {
            if (webcamTexture == null || !webcamTexture.isPlaying)
            {
                return null;
            }

            // Create or resize texture to match webcam texture
            if (cameraFrameTex == null || cameraFrameTex.width != webcamTexture.width || cameraFrameTex.height != webcamTexture.height)
            {
                cameraFrameTex = new Texture2D(webcamTexture.width, webcamTexture.height, TextureFormat.RGBA32, false);
            }

            try
            {
                Color32[] pixels = webcamTexture.GetPixels32();
                cameraFrameTex.SetPixels32(pixels);
                cameraFrameTex.Apply();
                return cameraFrameTex;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error capturing camera frame: {ex.Message}");
                return null;
            }
        }

        public Texture2D GetScreenFrame(bool captureDesktop)
        {
            try
            {
                if (captureDesktop)
                {
                    return CaptureDesktopFrame();
                }

                // In Unity, ScreenCapture.CaptureScreenshotAsTexture() captures the active application frame.
                // It is fully cross-platform and reliable.
                Texture2D screenTex = ScreenCapture.CaptureScreenshotAsTexture();
                if (screenTex != null)
                {
                    return screenTex;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error capturing screen: {ex.Message}");
            }
            return null;
        }

        private void Update()
        {
            CompleteDesktopCaptureIfReady();
        }

        private Texture2D CaptureDesktopFrame()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            CompleteDesktopCaptureIfReady();
            if (desktopFrameTex != null &&
                Time.realtimeSinceStartup - lastDesktopCaptureTime <
                    DesktopCaptureInterval)
            {
                return desktopFrameTex;
            }

            if (desktopCaptureProcess != null)
            {
                return desktopFrameTex;
            }

            try
            {
                Directory.CreateDirectory(
                    Application.temporaryCachePath);
                desktopCapturePath = Path.Combine(
                    Application.temporaryCachePath,
                    "jarvis-desktop-context.jpg");
                ProcessStartInfo startInfo =
                    new ProcessStartInfo
                    {
                        FileName = "/usr/sbin/screencapture",
                        Arguments =
                            "-x -D 1 -t jpg " +
                            QuoteArgument(desktopCapturePath),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true
                    };

                desktopCaptureProcess =
                    Process.Start(startInfo);
                desktopCaptureStartedTime =
                    Time.realtimeSinceStartup;
                if (desktopCaptureProcess == null)
                {
                    throw new InvalidOperationException(
                        "capture process did not start");
                }
            }
            catch (Exception ex)
            {
                ReportDesktopCaptureFailure(ex.Message);
            }
            return desktopFrameTex;
#else
            return null;
#endif
        }

        private void CompleteDesktopCaptureIfReady()
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            if (desktopCaptureProcess == null)
            {
                return;
            }

            if (!desktopCaptureProcess.HasExited)
            {
                if (Time.realtimeSinceStartup -
                        desktopCaptureStartedTime >
                    3f)
                {
                    try
                    {
                        desktopCaptureProcess.Kill();
                    }
                    catch
                    {
                    }
                    ReportDesktopCaptureFailure(
                        "desktop capture timed out");
                    DisposeDesktopCaptureProcess();
                }
                return;
            }

            try
            {
                string details =
                    desktopCaptureProcess.StandardError.ReadToEnd();
                if (desktopCaptureProcess.ExitCode != 0)
                {
                    throw new InvalidOperationException(details);
                }
                if (string.IsNullOrEmpty(desktopCapturePath) ||
                    !File.Exists(desktopCapturePath))
                {
                    throw new FileNotFoundException(
                        "desktop capture returned no image");
                }

                byte[] imageBytes =
                    File.ReadAllBytes(desktopCapturePath);
                if (desktopFrameTex == null)
                {
                    desktopFrameTex =
                        new Texture2D(
                            2,
                            2,
                            TextureFormat.RGB24,
                            false);
                }
                if (!desktopFrameTex.LoadImage(
                        imageBytes,
                        false))
                {
                    throw new InvalidOperationException(
                        "desktop image could not be decoded");
                }

                lastDesktopCaptureTime =
                    Time.realtimeSinceStartup;
                desktopCaptureWarningShown = false;
            }
            catch (Exception ex)
            {
                ReportDesktopCaptureFailure(ex.Message);
            }
            finally
            {
                DisposeDesktopCaptureProcess();
            }
#endif
        }

        private void ReportDesktopCaptureFailure(string details)
        {
            if (desktopCaptureWarningShown)
            {
                return;
            }
            Debug.LogWarning(
                "[ContextService] Full desktop capture failed. " +
                "Allow Screen Recording for Unity in macOS " +
                "Privacy & Security. " +
                details);
            desktopCaptureWarningShown = true;
        }

        private void DisposeDesktopCaptureProcess()
        {
            if (desktopCaptureProcess != null)
            {
                desktopCaptureProcess.Dispose();
                desktopCaptureProcess = null;
            }
            try
            {
                if (!string.IsNullOrEmpty(desktopCapturePath) &&
                    File.Exists(desktopCapturePath))
                {
                    File.Delete(desktopCapturePath);
                }
            }
            catch
            {
            }
            desktopCapturePath = null;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" +
                value.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"") +
                "\"";
        }

        private void OnDestroy()
        {
            StopCamera();
            if (desktopCaptureProcess != null &&
                !desktopCaptureProcess.HasExited)
            {
                try
                {
                    desktopCaptureProcess.Kill();
                }
                catch
                {
                }
            }
            DisposeDesktopCaptureProcess();
            if (cameraFrameTex != null)
            {
                Destroy(cameraFrameTex);
            }
            if (desktopFrameTex != null)
            {
                Destroy(desktopFrameTex);
            }
        }
    }
}
