using System;
using System.Collections;
using UnityEngine;

namespace AICompanion.Core
{
    public interface ILLMService
    {
        IEnumerator SendMessageCoroutine(string userMessage, Texture2D screenshot, Texture2D webcamFeed, Action<string> onComplete, Action<string> onError);
        IEnumerator SendDirectiveCoroutine(string directive, Action<string> onComplete, Action<string> onError);
    }

    public interface IVoiceService
    {
        IEnumerator GenerateSpeechCoroutine(string characterId, string text, Action<AudioClip> onComplete, Action<string> onError);
    }

    public interface IVRMService
    {
        void LoadVRM(string filePath, Action<GameObject> onLoaded, Action<string> onError);
        void LoadPlayerVRM(string filePath, Action<GameObject> onLoaded, Action<string> onError);
        void LoadWorldVRM(
            string characterId,
            string filePath,
            Vector3 position,
            Quaternion rotation,
            Action<GameObject> onLoaded,
            Action<string> onError);
        void SetExpression(string expressionName, float value);
        void PlayMouthVolume(float volume);
        void SetSpeaking(bool speaking);
        void SetAnimationEnabled(bool enabled);
        void SetSpeechBubblesVisible(bool visible);
        void SetSpeechBubbleMessage(string message);
        System.Collections.Generic.List<string>
            GetAvailableSatellites();
        bool CallSatellite(string satelliteId);
        void UndoLastPlacement();
        void WhistleForCamera();
        void SetCameraAttentionLocked(bool locked);
        void FocusSelectedCharacterForConversation();
        string GetPrimaryConversationCharacter();
        System.Collections.Generic.List<string>
            GetConversationCharactersInFrame();
        void SetCharacterSpeech(
            string characterId,
            string message,
            bool speaking);
        string GetWorldStateSummary();
        bool SetCharacterPoseByName(
            string characterId,
            string poseName);
        void CyclePose();
        System.Collections.Generic.List<string> GetAvailablePoses();
        void SetPose(int index);
    }

    public interface IContextService
    {
        void StartCamera();
        void StopCamera();
        Texture2D GetCameraFrame();
        Texture2D GetScreenFrame(bool captureDesktop);
    }

    public interface ISpeechToTextService
    {
        void StartRecording();
        void StopRecording(Action<string> onCompleted, Action<string> onError);
        bool IsRecording { get; }
        void SetListeningEnabled(bool enabled);
    }
}
