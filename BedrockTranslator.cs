//************************************************************************************************
// Copyright © 2020 Steven M Cohn.  All rights reserved.
//************************************************************************************************

using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime.CredentialManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ResxTranslator
{
    internal class BedrockTranslator : IDisposable
    {
        private readonly AmazonBedrockRuntimeClient _client;
        private readonly string _modelId = "us.anthropic.claude-sonnet-4-20250514-v1:0";
        private readonly List<Message> _conversationHistory;
        private bool _disposed = false;
        private bool _translationContextInitialized = false;
        private bool _detectionContextInitialized = false;

        public BedrockTranslator()
        {
            try
            {
                var chain = new CredentialProfileStoreChain();
                if (!chain.TryGetAWSCredentials("legacy", out var credentials))
                {
                    throw new BedrockConfigurationException("AWS credentials not found. Please configure your AWS credentials using the AWS CLI or SDK.");
                }

                _client = new AmazonBedrockRuntimeClient(credentials, Amazon.RegionEndpoint.USWest2);
                _conversationHistory = new List<Message>();
            }
            catch (Exception ex)
            {
                throw new BedrockConfigurationException(
                    "Failed to initialize AWS Bedrock client. Please ensure AWS credentials are configured correctly.", ex);
            }
        }

        public async Task<string> TranslateAsync(string text, string targetLanguage, string sourceLanguage = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            try
            {
                // Initialize translation context on first use
                if (!_translationContextInitialized)
                {
                    await InitializeTranslationContext();
                    _translationContextInitialized = true;
                }

                var prompt = CreateTranslationPrompt(text, targetLanguage, sourceLanguage);
                var userMessage = new Message
                {
                    Role = ConversationRole.User,
                    Content = new List<ContentBlock>
                    {
                        new ContentBlock { Text = prompt }
                    }
                };

                _conversationHistory.Add(userMessage);

                var request = new ConverseRequest
                {
                    ModelId = _modelId,
                    Messages = new List<Message>(_conversationHistory),
                    InferenceConfig = new InferenceConfiguration
                    {
                        MaxTokens = 4000,
                        Temperature = 0.1f,
                        TopP = 0.9f
                    }
                };

                var response = await _client.ConverseAsync(request);
                var translatedText = response?.Output?.Message?.Content?[0]?.Text?.Trim() ?? text;

                // Add assistant response to conversation history
                var assistantMessage = new Message
                {
                    Role = ConversationRole.Assistant,
                    Content = new List<ContentBlock>
                    {
                        new ContentBlock { Text = translatedText }
                    }
                };
                _conversationHistory.Add(assistantMessage);

                return translatedText;
            }
            catch (ValidationException ex)
            {
                throw new BedrockModelNotFoundException(_modelId, 
                    $"Claude model '{_modelId}' is not available in your AWS region. Please check your AWS Bedrock model access in the AWS Console.", ex);
            }
            catch (AccessDeniedException ex)
            {
                throw new BedrockAccessDeniedException(
                    "Access denied to AWS Bedrock. Please check your AWS credentials and IAM permissions (bedrock:InvokeModel required).", ex);
            }
            catch (ThrottlingException ex)
            {
                throw new BedrockRateLimitException(
                    "AWS Bedrock rate limit exceeded. Please wait a moment and try again.", ex);
            }
            catch (ServiceUnavailableException ex)
            {
                throw new BedrockServiceException("ServiceUnavailable", 
                    "AWS Bedrock service is temporarily unavailable. Please try again later.", ex);
            }
            catch (InternalServerException ex)
            {
                throw new BedrockServiceException("InternalServerError", 
                    "AWS Bedrock encountered an internal error. Please try again later.", ex);
            }
            catch (Amazon.Runtime.AmazonServiceException ex)
            {
                throw new BedrockServiceException(ex.ErrorCode, 
                    $"AWS Bedrock error ({ex.ErrorCode}): {ex.Message}", ex);
            }
            catch (Amazon.Runtime.AmazonClientException ex)
            {
                throw new BedrockConfigurationException(
                    "AWS client configuration error. Please check your AWS credentials and network connection.", ex);
            }
            catch (Exception ex)
            {
                throw new BedrockServiceException("UnknownError", 
                    $"Unexpected error during translation: {ex.Message}", ex);
            }
        }

        public async Task<string> DetectLanguageAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "en";

            try
            {
                // Use separate context for language detection to avoid polluting translation conversation
                var messages = new List<Message>();

                if (!_detectionContextInitialized)
                {
                    var systemMessage = new Message
                    {
                        Role = ConversationRole.Assistant,
                        Content = new List<ContentBlock>
                        {
                            new ContentBlock { Text = "You are a language detection expert. When given text, respond only with the ISO 639-1 language code (2 letters lowercase). Never provide explanations or additional text." }
                        }
                    };
                    messages.Add(systemMessage);
                    _detectionContextInitialized = true;
                }

                var prompt = $"Detect the language: \"{text}\"";
                var userMessage = new Message
                {
                    Role = ConversationRole.User,
                    Content = new List<ContentBlock>
                    {
                        new ContentBlock { Text = prompt }
                    }
                };
                messages.Add(userMessage);

                var request = new ConverseRequest
                {
                    ModelId = _modelId,
                    Messages = messages,
                    InferenceConfig = new InferenceConfiguration
                    {
                        MaxTokens = 10,
                        Temperature = 0.1f,
                        TopP = 0.9f
                    }
                };

                var response = await _client.ConverseAsync(request);
                var detectedLanguage = response?.Output?.Message?.Content?[0]?.Text?.Trim().ToLower() ?? "en";
                
                return detectedLanguage;
            }
            catch
            {
                return "en"; // Default to English if detection fails
            }
        }

        private async Task InitializeTranslationContext()
        {
            var systemMessage = new Message
            {
                Role = ConversationRole.Assistant,
                Content = new List<ContentBlock>
                {
                    new ContentBlock { Text = "You are a professional translator specializing in software localization. Your task is to translate user interface strings while preserving their meaning, tone, and technical accuracy. Always respond with only the translated text, no explanations or additional content." }
                }
            };

            _conversationHistory.Add(systemMessage);

            // Send a brief initialization exchange to establish context
            var initUserMessage = new Message
            {
                Role = ConversationRole.User,
                Content = new List<ContentBlock>
                {
                    new ContentBlock { Text = "I will be providing strings from a software application for translation. Please translate each one accurately while preserving technical terms and UI conventions." }
                }
            };

            _conversationHistory.Add(initUserMessage);

            var request = new ConverseRequest
            {
                ModelId = _modelId,
                Messages = new List<Message>(_conversationHistory),
                InferenceConfig = new InferenceConfiguration
                {
                    MaxTokens = 100,
                    Temperature = 0.1f,
                    TopP = 0.9f
                }
            };

            try
            {
                var response = await _client.ConverseAsync(request);
                var assistantResponse = response?.Output?.Message?.Content?[0]?.Text ?? "Understood. I'm ready to translate your software strings.";

                var assistantMessage = new Message
                {
                    Role = ConversationRole.Assistant,
                    Content = new List<ContentBlock>
                    {
                        new ContentBlock { Text = assistantResponse }
                    }
                };
                _conversationHistory.Add(assistantMessage);
            }
            catch
            {
                // If initialization fails, we'll still proceed with translations
                // Remove the user message since we didn't get a response
                _conversationHistory.RemoveAt(_conversationHistory.Count - 1);
            }
        }

        private string CreateTranslationPrompt(string text, string targetLanguage, string sourceLanguage)
        {
            var languageNames = new Dictionary<string, string>
            {
                {"en", "English"}, {"es", "Spanish"}, {"fr", "French"}, { "fr-ca", "French (Canada)"}, {"de", "German"}, {"it", "Italian"},
                {"pt", "Portuguese"}, {"ru", "Russian"}, {"ja", "Japanese"}, {"ko", "Korean"}, {"zh-CN", "Chinese (Simplified)"},
                {"zh-TW", "Chinese (Traditional)"}, {"ar", "Arabic"}, {"hi", "Hindi"}, {"nl", "Dutch"}, {"sv", "Swedish"},
                {"no", "Norwegian"}, {"da", "Danish"}, {"fi", "Finnish"}, {"pl", "Polish"}, {"cs", "Czech"},
                {"hu", "Hungarian"}, {"ro", "Romanian"}, {"bg", "Bulgarian"}, {"hr", "Croatian"}, {"sk", "Slovak"},
                {"sl", "Slovenian"}, {"et", "Estonian"}, {"lv", "Latvian"}, {"lt", "Lithuanian"}, {"he", "Hebrew"},
                {"th", "Thai"}, {"vi", "Vietnamese"}, {"tr", "Turkish"}, {"uk", "Ukrainian"}, {"el", "Greek"},
                {"ca", "Catalan"}, {"eu", "Basque"}, {"gl", "Galician"}, {"cy", "Welsh"}, {"ga", "Irish"},
                {"mt", "Maltese"}, {"is", "Icelandic"}, {"sq", "Albanian"}, {"mk", "Macedonian"}, {"sr", "Serbian"},
                {"bs", "Bosnian"}, {"be", "Belarusian"}, {"hy", "Armenian"}, {"ka", "Georgian"}, {"az", "Azerbaijani"},
                {"kk", "Kazakh"}, {"ky", "Kyrgyz"}, {"uz", "Uzbek"}, {"tk", "Turkmen"}, {"tg", "Tajik"},
                {"mn", "Mongolian"}, {"ur", "Urdu"}, {"fa", "Persian"}, {"ps", "Pashto"}, {"ku", "Kurdish"},
                {"am", "Amharic"}, {"sw", "Swahili"}, {"ha", "Hausa"}, {"yo", "Yoruba"}, {"ig", "Igbo"},
                {"zu", "Zulu"}, {"xh", "Xhosa"}, {"af", "Afrikaans"}, {"so", "Somali"}, {"rw", "Kinyarwanda"},
                {"mg", "Malagasy"}, {"bn", "Bengali"}, {"gu", "Gujarati"}, {"kn", "Kannada"}, {"ml", "Malayalam"},
                {"mr", "Marathi"}, {"ne", "Nepali"}, {"or", "Odia"}, {"pa", "Punjabi"}, {"sd", "Sindhi"},
                {"si", "Sinhala"}, {"ta", "Tamil"}, {"te", "Telugu"}, {"my", "Myanmar"}, {"km", "Khmer"},
                {"lo", "Lao"}, {"ms", "Malay"}, {"id", "Indonesian"}, {"tl", "Filipino"}, {"haw", "Hawaiian"},
                {"mi", "Maori"}, {"ceb", "Cebuano"}, {"eo", "Esperanto"}, {"la", "Latin"}, {"co", "Corsican"},
                {"fy", "Frisian"}, {"lb", "Luxembourgish"}, {"gd", "Scots Gaelic"}, {"st", "Sesotho"},
                {"sn", "Shona"}, {"tt", "Tatar"}, {"ug", "Uyghur"}, {"yi", "Yiddish"}
            };

            var targetLangName = languageNames.ContainsKey(targetLanguage) ? languageNames[targetLanguage] : targetLanguage;
            var sourceLangName = !string.IsNullOrEmpty(sourceLanguage) && sourceLanguage != "auto" 
                ? (languageNames.ContainsKey(sourceLanguage) ? languageNames[sourceLanguage] : sourceLanguage)
                : null;

            var prompt = sourceLangName != null
                ? $"Translate from {sourceLangName} to {targetLangName}: \"{text}\""
                : $"Translate to {targetLangName}: \"{text}\"";

            return prompt;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _client?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
