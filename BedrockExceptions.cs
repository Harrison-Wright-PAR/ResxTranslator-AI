//************************************************************************************************
// Copyright © 2020 Steven M Cohn.  All rights reserved.
//************************************************************************************************

using System;

namespace ResxTranslator
{
    /// <summary>
    /// Exception thrown when there are configuration issues with AWS Bedrock
    /// </summary>
    public class BedrockConfigurationException : Exception
    {
        public BedrockConfigurationException(string message) : base(message) { }
        public BedrockConfigurationException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when the requested model is not found or not available
    /// </summary>
    public class BedrockModelNotFoundException : Exception
    {
        public string ModelId { get; }

        public BedrockModelNotFoundException(string modelId, string message) : base(message) 
        {
            ModelId = modelId;
        }

        public BedrockModelNotFoundException(string modelId, string message, Exception innerException) : base(message, innerException) 
        {
            ModelId = modelId;
        }
    }

    /// <summary>
    /// Exception thrown when access to AWS Bedrock is denied
    /// </summary>
    public class BedrockAccessDeniedException : Exception
    {
        public BedrockAccessDeniedException(string message) : base(message) { }
        public BedrockAccessDeniedException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when rate limits are exceeded
    /// </summary>
    public class BedrockRateLimitException : Exception
    {
        public BedrockRateLimitException(string message) : base(message) { }
        public BedrockRateLimitException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown for general AWS Bedrock service errors
    /// </summary>
    public class BedrockServiceException : Exception
    {
        public string ErrorCode { get; }

        public BedrockServiceException(string errorCode, string message) : base(message) 
        {
            ErrorCode = errorCode;
        }

        public BedrockServiceException(string errorCode, string message, Exception innerException) : base(message, innerException) 
        {
            ErrorCode = errorCode;
        }
    }
}
