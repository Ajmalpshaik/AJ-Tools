using System;
using System.Collections.Generic;

namespace AJTools.GeminiShell.Models
{
    public class GeminiMessage
    {
        public string Role { get; set; } // "user" or "model"
        public string Content { get; set; }
    }

    public class GeminiRequest
    {
        public List<Content> contents { get; set; } = new List<Content>();
        public GenerationConfig generationConfig { get; set; } = new GenerationConfig();
    }

    public class Content
    {
        public string role { get; set; }
        public List<Part> parts { get; set; } = new List<Part>();
    }

    public class Part
    {
        public string text { get; set; }
    }

    public class GenerationConfig
    {
        public double temperature { get; set; } = 0.2;
    }

    public class GeminiResponse
    {
        public List<Candidate> candidates { get; set; }
    }

    public class Candidate
    {
        public Content content { get; set; }
    }
}
