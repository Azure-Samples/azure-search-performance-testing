// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Text;

namespace SemanticScholarDataUploader
{
    public class SemanticScholar
    {
        [JsonPropertyName("id")]
        public string id { get; set; }

        [JsonPropertyName("magId")]
        public string magId { get; set; }

        [JsonPropertyName("entities")]
        public string[] entities { get; set; }

        [JsonPropertyName("fieldsOfStudy")]
        public string[] fieldsOfStudy { get; set; }

        [JsonPropertyName("journalVolume")]
        public string journalVolume { get; set; }

        [JsonPropertyName("journalPages")]
        public string journalPages { get; set; }

        [JsonPropertyName("pmid")]
        public string pmid { get; set; }

        [JsonPropertyName("year")]
        public int? year { get; set; }

        [JsonPropertyName("s2Url")]
        public string s2Url { get; set; }

        [JsonPropertyName("s2PdfUrl")]
        public string s2PdfUrl { get; set; }

        [JsonPropertyName("journs2PdfUrlalPages")]
        public string journs2PdfUrlalPages { get; set; }

        [JsonPropertyName("journalName")]
        public string journalName { get; set; }

        [JsonPropertyName("paperAbstract")]
        public string paperAbstract { get; set; }

        [JsonPropertyName("title")]
        public string title { get; set; }

        [JsonPropertyName("doi")]
        public string doi { get; set; }

        [JsonPropertyName("doiUrl")]
        public string doiUrl { get; set; }

        [JsonPropertyName("venue")]
        public string venue { get; set; }

        [JsonPropertyName("outCitations")]
        public string[] outCitations { get; set; }

        [JsonPropertyName("inCitations")]
        public string[] inCitations { get; set; }

        [JsonPropertyName("pdfUrls")]
        public string[] pdfUrls { get; set; }

        [JsonPropertyName("sources")]
        public string[] sources { get; set; }

        [JsonPropertyName("authors")]
        public Author[] authors { get; set; }
    }

    public class Author
    {
        [JsonPropertyName("name")]
        public string name { get; set; }

        [JsonPropertyName("ids")]
        public string[] ids { get; set; }

    }
}

