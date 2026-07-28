using System;
using System.Collections;
using System.Text;
using UnityEngine;
using AICompanion.Core;

namespace AICompanion.Services
{
    public class AnimaleseVoiceService : MonoBehaviour, IVoiceService
    {
        private const bool DefaultShorten = true;  // Classic fast chatter

        public IEnumerator GenerateSpeechCoroutine(string characterId, string text, Action<AudioClip> onComplete, Action<string> onError)
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

            ResolveVoiceProfile(
                characterId,
                out float pitch,
                out float outputLetterSecs,
                out float lowPassBlend);
            int outputSamplesPerLetter = Mathf.FloorToInt(outputLetterSecs * sampleRate);   // 3307 samples

            // 5. Generate synthesized output samples
            int totalSamples = processedText.Length * outputSamplesPerLetter;
            float[] samples = new float[totalSamples];

            for (int cIndex = 0; cIndex < processedText.Length; cIndex++)
            {
                char c = processedText[cIndex];
                if (c >= 'A' && c <= 'Z')
                {
                    int letterOffset = c - 'A';
                    int libraryLetterStart = librarySamplesPerLetter * letterOffset;

                    int fadeSamples =
                        Mathf.Max(
                            1,
                            Mathf.RoundToInt(
                                sampleRate * 0.006f));
                    for (int i = 0; i < outputSamplesPerLetter; i++)
                    {
                        float sourceOffset = i * pitch;
                        int firstOffset =
                            Mathf.FloorToInt(
                                sourceOffset);
                        int secondOffset =
                            Mathf.Min(
                                firstOffset + 1,
                                librarySamplesPerLetter - 1);
                        float interpolation =
                            sourceOffset - firstOffset;
                        int firstIndex =
                            libraryLetterStart + firstOffset;
                        int secondIndex =
                            libraryLetterStart + secondOffset;
                        int destIndex = cIndex * outputSamplesPerLetter + i;

                        if (firstIndex >= 0 &&
                            secondIndex < librarySamples.Length)
                        {
                            float sample = Mathf.Lerp(
                                librarySamples[firstIndex],
                                librarySamples[secondIndex],
                                interpolation);
                            float fadeIn =
                                Mathf.Clamp01(
                                    i /
                                    (float)fadeSamples);
                            float fadeOut =
                                Mathf.Clamp01(
                                    (outputSamplesPerLetter -
                                     1 - i) /
                                    (float)fadeSamples);
                            samples[destIndex] =
                                sample *
                                Mathf.Min(
                                    fadeIn,
                                    fadeOut);
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

            // Remove DC offset and gently roll off the aliasing created by
            // pitching the letter bank upward. This prevents the faint
            // high-frequency whistle that otherwise survives between words.
            float mean = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                mean += samples[i];
            }
            mean /= Mathf.Max(1, samples.Length);

            float filtered = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float centered = samples[i] - mean;
                filtered +=
                    (centered - filtered) *
                    lowPassBlend;
                samples[i] = filtered;
            }

            // 6. Create and return the custom synthesized AudioClip
            AudioClip clip = AudioClip.Create("AnimaleseSpeech", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);

            onComplete?.Invoke(clip);
            yield break;
        }

        private static void ResolveVoiceProfile(
            string characterId,
            out float pitch,
            out float letterSeconds,
            out float lowPassBlend)
        {
            string normalized =
                (characterId ?? string.Empty)
                    .Trim()
                    .ToLowerInvariant()
                    .Replace("_jarvis", string.Empty)
                    .Replace(" jarvis", string.Empty);
            switch (normalized)
            {
                case "jojo":
                    pitch = 1.27f;
                    letterSeconds = 0.078f;
                    lowPassBlend = 0.40f;
                    break;
                case "spike":
                    pitch = 1.08f;
                    letterSeconds = 0.086f;
                    lowPassBlend = 0.31f;
                    break;
                case "charmy":
                default:
                    pitch = 1.48f;
                    letterSeconds = 0.070f;
                    lowPassBlend = 0.47f;
                    break;
            }
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
