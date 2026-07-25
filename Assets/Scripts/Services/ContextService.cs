using System;
using UnityEngine;
using AICompanion.Core;

namespace AICompanion.Services
{
    public class ContextService : MonoBehaviour, IContextService
    {
        private WebCamTexture webcamTexture;
        private Texture2D cameraFrameTex;

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

        private void OnDestroy()
        {
            StopCamera();
        }
    }
}
