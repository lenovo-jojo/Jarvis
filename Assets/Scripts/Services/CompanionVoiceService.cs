using System;
using System.Collections;
using UnityEngine;
using AICompanion.Core;

namespace AICompanion.Services
{
    public class CompanionVoiceService : MonoBehaviour, IVoiceService
    {
        private ElevenLabsVoiceService elevenLabsService;
        private AnimaleseVoiceService animaleseService;

        private void Awake()
        {
            elevenLabsService = gameObject.AddComponent<ElevenLabsVoiceService>();
            animaleseService = gameObject.AddComponent<AnimaleseVoiceService>();
        }

        public IEnumerator GenerateSpeechCoroutine(string text, Action<AudioClip> onComplete, Action<string> onError)
        {
            ServiceConfig config = ServiceConfig.Load();
            if (config != null && config.elevenLabsModelId == "animalese.js")
            {
                yield return animaleseService.GenerateSpeechCoroutine(text, onComplete, onError);
            }
            else
            {
                yield return elevenLabsService.GenerateSpeechCoroutine(text, onComplete, onError);
            }
        }
    }
}
