using System;
using System.Collections;
using System.Text;
using UnityEngine;
using AICompanion.Core;

namespace AICompanion.Services
{
    public class AnimaleseVoiceService : MonoBehaviour, IVoiceService
    {
        private const float DefaultPitch = 1.35f; // Classic cute high pitch
        private const bool DefaultShorten = true;  // Classic fast chatter

        public IEnumerator GenerateSpeechCoroutine(string text, Action<AudioClip> onComplete, Action<string> onError)
        {
            if (string.IsNullOrEmpty(text))
            {
                onError?.Invoke("Text is empty.");
                yield break;
            }

            // 1. Load the animalese letters library wav file
            AudioClip libraryClip = null;
#if UNITY_EDITOR
            libraryClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/animalese.wav");
#endif
            if (libraryClip == null)
            {
                libraryClip = Resources.Load<AudioClip>("animalese");
            }

            if (libraryClip == null)
            {
                onError?.Invoke("Could not find Assets/animalese.wav letters library. Please make sure the file is imported.");
                yield break;
            }

            // 2. Fetch float samples from the letters library
            int sampleRate = libraryClip.frequency;
            float[] librarySamples = new float[libraryClip.samples];
            libraryClip.GetData(librarySamples, 0);

            // 3. Process the script text
            string processedText = ShortenText(text, DefaultShorten).ToUpperInvariant();

            if (string.IsNullOrEmpty(processedText))
            {
                onError?.Invoke("No pronouncable characters found.");
                yield break;
            }

            // 4. Set timing constants matching animalese.js exactly
            float libraryLetterSecs = 0.15f;
            int librarySamplesPerLetter = Mathf.FloorToInt(libraryLetterSecs * sampleRate); // 6615 samples

            float outputLetterSecs = 0.075f;
            int outputSamplesPerLetter = Mathf.FloorToInt(outputLetterSecs * sampleRate);   // 3307 samples

            // 5. Generate synthesized output samples
            int totalSamples = processedText.Length * outputSamplesPerLetter;
            float[] samples = new float[totalSamples];

            float pitch = DefaultPitch;

            for (int cIndex = 0; cIndex < processedText.Length; cIndex++)
            {
                char c = processedText[cIndex];
                if (c >= 'A' && c <= 'Z')
                {
                    int letterOffset = c - 'A';
                    int libraryLetterStart = librarySamplesPerLetter * letterOffset;

                    for (int i = 0; i < outputSamplesPerLetter; i++)
                    {
                        int sampleOffset = Mathf.FloorToInt(i * pitch);
                        // Wrap to avoid index out of bounds for the 0.15s letter sample
                        sampleOffset = sampleOffset % librarySamplesPerLetter;

                        int targetIndex = libraryLetterStart + sampleOffset;
                        int destIndex = cIndex * outputSamplesPerLetter + i;

                        if (targetIndex >= 0 && targetIndex < librarySamples.Length)
                        {
                            samples[destIndex] = librarySamples[targetIndex];
                        }
                        else
                        {
                            samples[destIndex] = 0f;
                        }
                    }
                }
                else
                {
                    // Non-pronounceable character or space: fill output letter samples with silence (0f)
                    for (int i = 0; i < outputSamplesPerLetter; i++)
                    {
                        int destIndex = cIndex * outputSamplesPerLetter + i;
                        samples[destIndex] = 0f;
                    }
                }
            }

            // 6. Create and return the custom synthesized AudioClip
            AudioClip clip = AudioClip.Create("AnimaleseSpeech", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);

            onComplete?.Invoke(clip);
            yield break;
        }

        private string ShortenText(string text, bool shorten)
        {
            if (!shorten) return text;
            if (string.IsNullOrEmpty(text)) return "";

            // Replace non-alphabet characters with space
            StringBuilder sb = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsLetter(c))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append(' ');
                }
            }

            string processed = sb.ToString();
            string[] words = processed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder result = new StringBuilder();

            foreach (var word in words)
            {
                if (word.Length > 1)
                {
                    result.Append(word[0]);
                    result.Append(word[word.Length - 1]);
                }
                else if (word.Length == 1)
                {
                    result.Append(word[0]);
                }
            }

            return result.ToString();
        }
    }
}
