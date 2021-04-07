// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// This application contains information on how to create an Azure Cognitive Search Index with data provided 
// by Semantic Scholar http://s2-public-api-prod.us-west-2.elasticbeanstalk.com/corpus/download/
// For uploading the data, we will use the Azure Cognitive Search SDK because it allows for retries and 
// exponential backoff. The average document size is ~3 KB Each corpus file (e.g. s2-corpus-000.gz) has 
// ~998,537 documents and is ~1,680MB in raw text (1.68KB / DOC)

using Azure;
using Azure.Core.Serialization;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SemanticScholarDataUploader
{
    class Program
    {
        //Set this to the number of documents you need in the search index
        //NOTE: One doc == ~3KB
        private static decimal DocumentsToUpload = 1000000;

        private static string ServiceName = "[Search Service Name]";
        private static string ApiKey = "[Search Admin API KEY]";
        private static string IndexName = "semanticscholar";

        private static string DownloadDir = @"c:\temp\delete\data";
        private static string SemanticScholarDownloadUrl = "https://s3-us-west-2.amazonaws.com/ai2-s2-research-public/open-corpus/2020-05-27/";
        private static decimal ApproxDocumentsPerFile = 1000000;
        private static int MaxParallelUploads = 8;
        private static int BatchUploadCounter = 0;
        private static int MaxBatchSize = 500;


        static void Main(string[] args)
        {
            // Create a SearchIndexClient to send create/delete index commands
            Uri serviceEndpoint = new Uri($"https://{ServiceName}.search.windows.net/");
            AzureKeyCredential credential = new AzureKeyCredential(ApiKey);
            SearchIndexClient idxclient = new SearchIndexClient(serviceEndpoint, credential);

            // Create a SearchClient to load and query documents
            SearchClient srchclient = new SearchClient(serviceEndpoint, IndexName, credential);

            // Delete index if it exists
            DeleteIndexIfExists(IndexName, idxclient);

            // Delete index if it exists
            CreateIndex(IndexName, idxclient);

            // Deleete and Re-create the data download dir
            ResetDownloadDir();

            var filesToDownload = Convert.ToInt32(Math.Ceiling(DocumentsToUpload / ApproxDocumentsPerFile));
            DownloadFiles(filesToDownload);

            UploadDocuments(srchclient, filesToDownload);
        }

        // Delete the index to reuse its name
        private static void DeleteIndexIfExists(string indexName, SearchIndexClient idxclient)
        {
            Console.WriteLine("{0}", "Deleting index...\n");
            idxclient.GetIndexNames();
            {
                idxclient.DeleteIndex(indexName);
            }
        }

        // Create the index
        private static void CreateIndex(string indexName, SearchIndexClient idxclient)
        {
            // Define an index schema and create the index
            Console.WriteLine("{0}", "Creating index...\n");
            var index = new SearchIndex(indexName)
            {
                Fields =
                    {
                        new SimpleField("id", SearchFieldDataType.String) {IsKey=true, IsFilterable=true},
                        new SimpleField("magId", SearchFieldDataType.String) {IsFilterable=true},
                        new SearchableField("entities", true) {IsFilterable=true, IsFacetable=true, AnalyzerName="en.microsoft"},
                        new SearchableField("fieldsOfStudy", true) {IsFilterable=true, IsFacetable=true },
                        new SimpleField("journalVolume", SearchFieldDataType.String) {IsFilterable=false, IsFacetable=false},
                        new SimpleField("journalPages", SearchFieldDataType.String) {IsFilterable=false, IsFacetable=false},
                        new SimpleField("pmid", SearchFieldDataType.String) {IsFilterable=false, IsFacetable=false},
                        new SimpleField("year", SearchFieldDataType.Int32) { IsFilterable = true, IsSortable = true, IsFacetable = true },
                        new SimpleField("s2Url", SearchFieldDataType.String) {IsFilterable=false, IsFacetable=false},
                        new SimpleField("s2PdfUrl", SearchFieldDataType.String) {IsFilterable=false, IsFacetable=false},
                        new SimpleField("journs2PdfUrlalPages", SearchFieldDataType.String) {IsFilterable=false, IsFacetable=false},
                        new SearchableField("journalName") {IsFilterable=true, IsFacetable=true, AnalyzerName="en.microsoft"},
                        new SearchableField("paperAbstract") {IsFilterable=false, IsFacetable=false, AnalyzerName="en.microsoft"},
                        new SearchableField("title") {IsFilterable=false, IsFacetable=false, AnalyzerName="en.microsoft"},
                        new SimpleField("doi", SearchFieldDataType.String) {IsFilterable=false, IsFacetable=false},
                        new SimpleField("doiUrl", SearchFieldDataType.String) {IsFilterable=false, IsFacetable=false},
                        new SearchableField("venue") {IsFilterable=true, IsFacetable=true, AnalyzerName="en.microsoft"},
                        new SearchableField("outCitations", true) {IsFilterable=true, IsFacetable=true },
                        new SearchableField("inCitations", true) {IsFilterable=false, IsFacetable=false },
                        new SearchableField("pdfUrls", true) {IsFilterable=false, IsFacetable=false },
                        new SearchableField("sources", true) {IsFilterable=true, IsFacetable=true },
                        new ComplexField("authors", collection: true)
                        {
                            Fields =
                            {
                                new SearchableField("name") {IsFilterable=true, IsFacetable=true, AnalyzerName="en.microsoft"},
                                new SearchableField("ids", true) {IsFilterable=true, IsFacetable=true }
                            }
                        }
                }
            };

            idxclient.CreateIndex(index);
        }

        static void UploadDocuments(SearchClient srchclient, int FileCount)
        {
            var docCounter = 0;
            var batchJobs = new List<IndexDocumentsBatch<SemanticScholar>>();
            var batch = new IndexDocumentsBatch<SemanticScholar>();
            
            Console.WriteLine("Creating batches for upload...");
            for (var fileNum = 0; fileNum < FileCount; fileNum++)
            {
                var paddedFileNum = fileNum.ToString().PadLeft(3, '0');
                var baseFileName = "s2-corpus-" + paddedFileNum + ".gz";
                var fileToProcess = Path.Combine(DownloadDir, baseFileName).Replace(".gz", "");
                const Int32 BufferSize = 128;
                using (var fileStream = File.OpenRead(fileToProcess))
                using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, BufferSize))
                {
                    String line;

                    while ((line = streamReader.ReadLine()) != null)
                    {
                        docCounter += 1;
                        if (docCounter == DocumentsToUpload)
                            break;
                        var ssDoc = JsonConvert.DeserializeObject<SemanticScholar>(line);
                        batch.Actions.Add(IndexDocumentsAction.Upload(ssDoc));
                        if (docCounter % MaxBatchSize == 0)
                        {
                            batchJobs.Add(batch);
                            batch = new IndexDocumentsBatch<SemanticScholar>();
                            if (batchJobs.Count % 100 == 0)
                                Console.WriteLine("Created {0} batches...", batchJobs.Count);

                        }

                    }
                }

                ParallelBatchApplication(batchJobs, srchclient);

                batchJobs.Clear();
                batch = new IndexDocumentsBatch<SemanticScholar>();

                if (docCounter == DocumentsToUpload)
                    break;

            }

            if (batch.Actions.Count > 0)
                batchJobs.Add(batch);

            ParallelBatchApplication(batchJobs, srchclient);

        }

        static void ParallelBatchApplication(List<IndexDocumentsBatch<SemanticScholar>> batchJobs, SearchClient srchclient)
        {
            // Apply a set of batches in parallel
            var idxoptions = new IndexDocumentsOptions { ThrowOnAnyError = true };
            Parallel.ForEach(batchJobs,
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallelUploads },
                (b) =>
                {
                    Interlocked.Increment(ref BatchUploadCounter);
                    Console.WriteLine("Uploading Batch {0} with doc count {1}", BatchUploadCounter.ToString(), (BatchUploadCounter * MaxBatchSize).ToString());
                    srchclient.IndexDocuments(b, idxoptions);

                });
        }

        static void ResetDownloadDir()
        {
            if (Directory.Exists(DownloadDir))
                Directory.Delete(DownloadDir, true);
            Directory.CreateDirectory(DownloadDir);
        }


        static void DownloadFiles(int FileCount)
        {
            // Download the semantic scholar files
            for (var fileNum = 0; fileNum < FileCount; fileNum++)
            {
                var client = new WebClient();
                var paddedFileNum = fileNum.ToString().PadLeft(3, '0');
                var baseFileName = "s2-corpus-" + paddedFileNum + ".gz";
                var downloadFileUrl = SemanticScholarDownloadUrl + baseFileName;
                Console.WriteLine("Downloading File: {0}", baseFileName);

                client.DownloadFile(downloadFileUrl, Path.Combine(DownloadDir, baseFileName));

                // Decompress the file
                Console.WriteLine("Decompressing File: {0}", baseFileName);
                using (FileStream fInStream = new FileStream(Path.Combine(DownloadDir, baseFileName), FileMode.Open, FileAccess.Read))
                {
                    using (GZipStream zipStream = new GZipStream(fInStream, CompressionMode.Decompress))
                    {
                        using (FileStream fOutStream = new FileStream(Path.Combine(DownloadDir, baseFileName).Replace(".gz", ""),
                        FileMode.Create, FileAccess.Write))
                        {
                            byte[] tempBytes = new byte[4096];
                            int i;
                            while ((i = zipStream.Read(tempBytes, 0, tempBytes.Length)) != 0)
                            {
                                fOutStream.Write(tempBytes, 0, i);
                            }
                        }
                    }
                }

                //Remove local gz file
                Console.WriteLine("Removing File: {0}", baseFileName);
                File.Delete(Path.Combine(DownloadDir, baseFileName));
            }
        }

    }
}
